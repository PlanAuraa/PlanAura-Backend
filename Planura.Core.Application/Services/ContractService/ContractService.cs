using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Planura.Core.Application.Abstraction.Contract;
using Planura.Core.Application.Models;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services.Contract;

/// <summary>
/// Orchestrates AI document generation for every contract type Planura issues: the Event Booking
/// Contract (Client vs Vendor) and the Vendor Partnership Agreement (Planura vs Vendor).
/// <para>
/// The two documents are deliberately generated very differently, because they are different kinds of
/// document. The partnership agreement is a platform-wide instrument: every vendor signs substantially
/// the same terms, so a fixed clause list is correct there. The booking contract governs one
/// negotiated transaction, so it is generated from a <see cref="ContractGenerationContext"/> through a
/// two-stage pipeline - extract the deal, then draft from the extracted deal - with no fixed clause
/// list at all, and the result is validated back against the source facts before it is rendered.
/// </para>
/// <para>
/// Holds no Gemini HTTP or QuestPDF layout knowledge itself - that stays in
/// <see cref="IGeminiService"/> and <see cref="IPdfService"/>.
/// </para>
/// </summary>
public class ContractService : IContractService
{
    /// <summary>Analysis is short and benefits from consistency; drafting needs room for a full contract.</summary>
    private const int DealAnalysisMaxTokens = 6144;
    private const int ContractDraftMaxTokens = 16384;

    /// <summary>
    /// Low but not zero. Variation between contracts must come from the input facts, not from
    /// sampling - but drafting still needs enough latitude to phrase genuinely different clause sets
    /// naturally rather than snapping to the most templated wording it knows.
    /// </summary>
    private const double DealAnalysisTemperature = 0.2;
    private const double ContractDraftTemperature = 0.45;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions AnalysisEchoOptions = new()
    {
        WriteIndented = true
    };

    private readonly IGeminiService _geminiService;
    private readonly IPdfService _pdfService;
    private readonly ILogger<ContractService> _logger;

    public ContractService(IGeminiService geminiService, IPdfService pdfService, ILogger<ContractService> logger)
    {
        _geminiService = geminiService;
        _pdfService = pdfService;
        _logger = logger;
    }

    // ============================================================= Event Booking Contract

    public async Task<ContractDocumentDto> GenerateBookingContractAsync(
        ContractGenerationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.ContractId))
        {
            context.ContractId = GenerateDocumentId("CN");
        }

        if (context.Booking.EventDate is { } eventDate &&
            eventDate < DateOnly.FromDateTime(DateTime.UtcNow.Date.AddYears(-1)))
        {
            throw new BadRequestExeption("Event date is not valid.");
        }

        // Logged at the boundary so two generation requests can be compared at a glance and proven to
        // have carried different inputs. Ids and deal shape only - no personal data, no prompt text.
        _logger.LogInformation(
            "Generating booking contract {ContractId}. Deal signature: {DealSignature}",
            context.ContractId, context.BuildDiagnosticSignature());

        var analysis = await ExtractDealAsync(context, cancellationToken);
        var analysisJson = JsonSerializer.Serialize(analysis, AnalysisEchoOptions);

        _logger.LogDebug(
            "Contract {ContractId} deal analysis: {ScopeCount} scope items, {DeliverableCount} deliverables, " +
            "{ClauseCount} clauses planned, {OpenPointCount} open points.",
            context.ContractId, analysis.ScopeItems.Count, analysis.Deliverables.Count,
            analysis.RequiredClauses.Count, analysis.OpenPoints.Count);

        var draft = await DraftAndValidateAsync(context, analysisJson, cancellationToken);
        var pdfModel = BuildBookingPdfModel(context, draft);

        return await RenderPdfAsync(pdfModel, context.ContractId, "Contract.pdf");
    }

    public Task<ContractDocumentDto> GenerateBookingContractAsync(
        GenerateContractDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return GenerateBookingContractAsync(MapToContext(dto), cancellationToken);
    }

    /// <summary>
    /// Stage 1 - read the fact sheet and state what was actually agreed. Drafting straight from raw
    /// facts is what produces template-shaped output: the model reaches for the shape of contract it
    /// knows before it has worked out what this particular deal contains. Forcing an explicit reading
    /// first means the draft argues from these facts.
    /// </summary>
    private async Task<ContractDealAnalysis> ExtractDealAsync(
        ContractGenerationContext context, CancellationToken cancellationToken)
    {
        var json = await _geminiService.GenerateTextAsync(new GeminiTextGenerationRequest
        {
            SystemInstruction = ContractPrompts.DealAnalysisSystemInstruction,
            Prompt = ContractPrompts.BuildDealAnalysisPrompt(context),
            Temperature = DealAnalysisTemperature,
            MaxOutputTokens = DealAnalysisMaxTokens,
            ResponseMimeType = "application/json",
            ResponseSchema = ContractPrompts.DealAnalysisSchema
        }, cancellationToken);

        var analysis = Deserialize<ContractDealAnalysis>(json, context.ContractId, "deal analysis");

        // The platform's own record of what the client asked for is authoritative; the model's reading
        // supplements it but must never quietly drop a stated requirement.
        foreach (var requirement in context.ClientRequirements)
        {
            if (!analysis.ClientRequirements.Any(r => string.Equals(r, requirement, StringComparison.OrdinalIgnoreCase)))
            {
                analysis.ClientRequirements.Add(requirement);
            }
        }

        return analysis;
    }

    /// <summary>
    /// Stage 2 - draft, then check the draft against the facts. One targeted regeneration is allowed,
    /// naming exactly what was missed. A draft still missing a critical fact after that is refused
    /// rather than shown to a client about to pay against it.
    /// </summary>
    private async Task<ContractDraft> DraftAndValidateAsync(
        ContractGenerationContext context, string analysisJson, CancellationToken cancellationToken)
    {
        var draft = await DraftAsync(
            context, ContractPrompts.BuildContractDraftingPrompt(context, analysisJson), cancellationToken);

        var validation = ContractFactValidator.Validate(context, draft);
        if (validation.IsValid)
        {
            return draft;
        }

        _logger.LogWarning(
            "Contract {ContractId} draft omitted {MissingCount} source fact(s); regenerating once. Missing: {MissingFacts}",
            context.ContractId, validation.AllMissing.Count, string.Join("; ", validation.AllMissing));

        var repaired = await DraftAsync(
            context,
            ContractPrompts.BuildFactRepairPrompt(context, analysisJson, validation.AllMissing),
            cancellationToken);

        var revalidation = ContractFactValidator.Validate(context, repaired);

        if (revalidation.MissingCritical.Count > 0)
        {
            _logger.LogError(
                "Contract {ContractId} failed fact validation after regeneration. Missing critical facts: {MissingFacts}",
                context.ContractId, string.Join("; ", revalidation.MissingCritical));

            throw new BadRequestExeption(
                "The contract could not be generated accurately from this booking's details. Please try again.");
        }

        if (revalidation.MissingImportant.Count > 0)
        {
            // Not worth blocking a booking over, but it must be visible: a recurring entry here means
            // a fact is reaching the prompt in a form the model keeps declining to use.
            _logger.LogWarning(
                "Contract {ContractId} generated with {MissingCount} unstated non-critical fact(s): {MissingFacts}",
                context.ContractId, revalidation.MissingImportant.Count, string.Join("; ", revalidation.MissingImportant));
        }

        return repaired;
    }

    private async Task<ContractDraft> DraftAsync(
        ContractGenerationContext context, string prompt, CancellationToken cancellationToken)
    {
        var json = await _geminiService.GenerateTextAsync(new GeminiTextGenerationRequest
        {
            SystemInstruction = ContractPrompts.ContractDraftingSystemInstruction,
            Prompt = prompt,
            Temperature = ContractDraftTemperature,
            MaxOutputTokens = ContractDraftMaxTokens,
            ResponseMimeType = "application/json",
            ResponseSchema = ContractPrompts.ContractDraftSchema
        }, cancellationToken);

        var draft = Deserialize<ContractDraft>(json, context.ContractId, "contract draft");

        draft.Sections = draft.Sections
            .Where(s => !string.IsNullOrWhiteSpace(s.Title) && (s.Paragraphs.Count > 0 || s.Items.Count > 0))
            .ToList();

        if (draft.Sections.Count == 0)
        {
            throw new AiProviderUnavailableExeption(
                "The AI assistant returned an empty document. Please try again.");
        }

        return draft;
    }

    private ContractPdfModel BuildBookingPdfModel(ContractGenerationContext context, ContractDraft draft)
    {
        return new ContractPdfModel
        {
            ContractId = context.ContractId,
            GeneratedDate = context.GeneratedAt,
            DocumentTitle = string.IsNullOrWhiteSpace(draft.Title) ? "Event Booking Contract" : draft.Title.Trim(),
            DocumentTagline = "A binding agreement between the Client and the Vendor, facilitated by Planura.",
            IntroParagraph = string.IsNullOrWhiteSpace(draft.Preamble)
                ? "This contract sets out the terms agreed between the Client and the Vendor for the service " +
                  "described below, facilitated through the Planura platform."
                : draft.Preamble.Trim(),
            Sections = draft.Sections
                .Select(s => new ContractSectionContent
                {
                    Title = s.Title!.Trim(),
                    Paragraphs = s.Paragraphs.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList(),
                    Items = s.Items.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).ToList()
                })
                .ToList(),
            PartyA = new ContractPartyDto
            {
                Label = "CLIENT",
                Name = context.Client.LegalName ?? "N/A",
                Email = context.Client.Email,
                Phone = context.Client.Phone,
                Address = context.Client.Address ?? context.Client.City,
                RepresentativeName = context.Client.RepresentativeName
            },
            PartyB = new ContractPartyDto
            {
                Label = "VENDOR",
                Name = context.Vendor.LegalName ?? "N/A",
                Email = context.Vendor.Email,
                Phone = context.Vendor.Phone,
                Address = context.Vendor.Address ?? context.Vendor.City,
                RepresentativeName = context.Vendor.RepresentativeName
            },
            // Built server-side from the context, never from model output, so the cover page facts are
            // guaranteed to be the real ones regardless of what the body says.
            SummaryItems = BuildSummaryItems(context)
        };
    }

    /// <summary>
    /// Cover-page facts, drawn only from values that actually exist. Absent facts drop out entirely
    /// rather than printing "N/A", so the summary reflects what this booking really specifies.
    /// </summary>
    private static List<ContractSummaryItem> BuildSummaryItems(ContractGenerationContext context)
    {
        var items = new List<ContractSummaryItem>();

        void Add(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                items.Add(new ContractSummaryItem(label, value));
            }
        }

        Add("Service", context.Service.Category);
        Add("Package", context.Service.PackageTitle);
        Add("Event Type", context.Booking.EventType);
        Add("Event Date", context.Booking.EventDate?.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture));
        Add("Duration", context.Booking.DurationHours is { } h
            ? $"{h.ToString("0.##", CultureInfo.InvariantCulture)} hours"
            : null);
        Add("Location", context.Booking.LocationDetail ?? context.Booking.City);
        Add("Guest Count", context.Booking.GuestCount?.ToString(CultureInfo.InvariantCulture));
        Add("Agreed Price", context.Financials.TotalAmount is { } total
            ? $"{total.ToString("N2", CultureInfo.InvariantCulture)} {context.Currency}"
            : null);

        if (context.Financials.IsDepositSchedule == true && context.Financials.DepositAmount is { } deposit)
        {
            Add("Deposit Now", $"{deposit.ToString("N2", CultureInfo.InvariantCulture)} {context.Currency}");
            Add("Balance Due", context.Financials.RemainderAmount is { } remainder
                ? $"{remainder.ToString("N2", CultureInfo.InvariantCulture)} {context.Currency}"
                : null);
        }

        Add("Contract ID", context.ContractId);

        return items;
    }

    /// <summary>
    /// Adapts the standalone endpoint's flat DTO onto the same context the booking flow builds, so
    /// there is exactly one booking-contract pipeline. The context is thinner here by nature - there
    /// is no package, slot or payment plan to draw on - and the prompt simply omits what is absent
    /// rather than inviting the model to fill the gaps.
    /// </summary>
    private static ContractGenerationContext MapToContext(GenerateContractDto dto)
    {
        var currency = string.IsNullOrWhiteSpace(dto.Currency) ? "EGP" : dto.Currency.ToUpperInvariant();

        return new ContractGenerationContext
        {
            ContractId = GenerateDocumentId("CN"),
            GeneratedAt = DateTimeOffset.UtcNow,
            Currency = currency,
            Client = new ContractPartyContext
            {
                LegalName = dto.ClientName,
                RepresentativeName = dto.ClientRepresentativeName,
                Email = dto.ClientEmail,
                Phone = dto.ClientPhone,
                Address = dto.ClientAddress
            },
            Vendor = new ContractPartyContext
            {
                LegalName = dto.VendorName,
                RepresentativeName = dto.VendorRepresentativeName,
                Email = dto.VendorEmail,
                Phone = dto.VendorPhone,
                Address = dto.VendorAddress
            },
            Booking = new ContractBookingContext
            {
                EventType = dto.EventType,
                EventDate = dto.EventDate,
                LocationDetail = dto.EventLocation,
                GuestCount = dto.GuestCount
            },
            Financials = new ContractFinancialContext
            {
                TotalAmount = dto.Price,
                PackageBasePrice = dto.Price
            },
            ClientNote = dto.AdditionalTerms
        };
    }

    // ============================================================= Vendor Partnership Agreement

    public async Task<ContractDocumentDto> GenerateVendorPartnershipContractAsync(GenerateVendorPartnershipDto dto, CancellationToken cancellationToken = default)
    {
        var contractId = GenerateDocumentId("VP");
        var generatedDate = DateTimeOffset.UtcNow;
        var effectiveDate = dto.EffectiveDate ?? DateOnly.FromDateTime(generatedDate.UtcDateTime);

        _logger.LogInformation(
            "Generating vendor partnership agreement {ContractId} for vendor {VendorName}.",
            contractId, dto.VendorName);

        var contractBody = await GenerateTextAsync(
            BuildVendorPartnershipSystemPrompt(),
            BuildVendorPartnershipUserPrompt(dto, contractId, generatedDate, effectiveDate),
            cancellationToken,
            maxOutputTokens: 8192);

        var pdfModel = new ContractPdfModel
        {
            ContractId = contractId,
            GeneratedDate = generatedDate,
            DocumentTitle = "Vendor Partnership Agreement",
            DocumentTagline = "A partnership agreement between Planura, the marketplace operator, and the Vendor, an independent contractor.",
            IntroParagraph =
                "This agreement governs the Vendor's relationship with Planura as a marketplace partner. It is " +
                "independent of, and does not form part of, any individual Client booking or Event Booking Contract.",
            ContractBody = contractBody,
            PartyA = new ContractPartyDto
            {
                Label = "PLANURA",
                Name = "Planura",
                RepresentativeName = dto.PlanuraRepresentativeName
            },
            PartyB = new ContractPartyDto
            {
                Label = "VENDOR",
                Name = dto.VendorName,
                Email = dto.VendorEmail,
                Phone = dto.VendorPhone,
                Address = dto.VendorAddress,
                RepresentativeName = dto.VendorRepresentativeName
            },
            SummaryItems = new List<ContractSummaryItem>
            {
                new("Vendor Business Name", dto.VendorName),
                new("Vendor Category", N(dto.VendorCategory)),
                new("Effective Date", effectiveDate.ToString("MMMM d, yyyy")),
                new(
                    "Commission Rate",
                    dto.CommissionRatePercent.HasValue
                        ? $"{dto.CommissionRatePercent.Value:0.##}%"
                        : "Per Planura's commission schedule"),
                new("Contract ID", contractId)
            }
        };

        return await RenderPdfAsync(pdfModel, contractId, "VendorPartnershipAgreement.pdf");
    }

    private static string BuildVendorPartnershipSystemPrompt() => """
        You are a senior commercial lawyer drafting a formal Vendor Partnership Agreement for
        Planura, an online marketplace that connects Clients planning events with independent
        service Vendors (catering, venues, photography, decor, entertainment, and similar
        categories). Planura operates the marketplace platform; it does not itself provide the
        Vendor's services.

        Write in professional, precise legal English suitable for a real, enforceable commercial
        agreement between a platform operator and an independent business partner.

        ABSOLUTE RULES - DO NOT BREAK THESE:
        1. This is NOT a booking contract and must never be confused with one. It is a standalone
           platform partnership agreement between Planura and the Vendor only.
        2. The Client must NEVER appear as a contracting party, and must not be named or implied to
           have signed or agreed to any term of this document. The Client is a third party who may
           book the Vendor's services through the platform, nothing more.
        3. Planura is the marketplace operator/facilitator. The Vendor is at all times an
           independent contractor and is never described as an employee, partner in the legal
           partnership sense, joint venturer, or agent of Planura.
        4. Planura does NOT guarantee the Vendor any bookings, revenue, or minimum volume of
           business. State this explicitly.
        5. Planura may suspend or terminate the Vendor's account for violations of Planura's
           policies. State this explicitly, including that suspension may be immediate for serious
           violations.
        6. The Vendor is solely responsible for delivering the services it offers to Clients,
           including quality, safety, and legality of those services.
        7. Planura may collect commissions from Vendor bookings according to Planura's prevailing
           commission/pricing policy.
        8. Never invent or assume any fact that was not explicitly provided to you: no invented
           addresses, emails, phone numbers, representative names, dates, or commission
           percentages. If a fact was provided to you as "N/A", write it into the agreement exactly
           as "N/A" - never replace "N/A" with a plausible-sounding guess. If no specific commission
           percentage was provided, refer generically to "Planura's prevailing commission schedule,
           as published in the Vendor's dashboard and updated from time to time" rather than stating
           a number.
        9. The governing law of this agreement is always the Arab Republic of Egypt, regardless of
           where the Vendor is located.
        10. Output ONLY the agreement text itself. Do not include any preamble, explanation,
            apology, or commentary before or after the agreement. Do not use markdown formatting,
            code fences, asterisks, or bullet characters of any kind.

        REQUIRED STRUCTURE AND FORMATTING:
        - Begin with a short introductory paragraph identifying this as a Vendor Partnership
          Agreement between Planura and the Vendor, distinct from any individual booking contract.
        - Divide the body into major sections. Each section MUST start on its own line using
          exactly this format: "SECTION <n>: <TITLE IN UPPERCASE>", followed by a blank line and
          then the section's plain-text paragraphs. Do not number sections any other way.
        - Within a section, if a numbered or lettered list is genuinely useful, format each list
          line starting with "(a)", "(b)", "(c)" etc. on its own line - never use markdown bullets
          or asterisks.
        - Include exactly these 28 sections, in this order, using these exact titles:
          SECTION 1: AGREEMENT TITLE AND NATURE OF THIS AGREEMENT
          SECTION 2: PARTIES TO THIS AGREEMENT
          SECTION 3: DEFINITIONS
          SECTION 4: PURPOSE OF THE AGREEMENT
          SECTION 5: VENDOR ONBOARDING REQUIREMENTS
          SECTION 6: VENDOR OBLIGATIONS
          SECTION 7: PLANURA'S RESPONSIBILITIES
          SECTION 8: COMMISSION AND PAYMENT TERMS
          SECTION 9: BOOKING PROCESS
          SECTION 10: COMMUNICATION RULES
          SECTION 11: VENDOR PROFILE AND CONTENT OWNERSHIP
          SECTION 12: SERVICE QUALITY STANDARDS
          SECTION 13: CUSTOMER REVIEWS AND RATINGS
          SECTION 14: CANCELLATION POLICY
          SECTION 15: REFUND HANDLING
          SECTION 16: INTELLECTUAL PROPERTY
          SECTION 17: CONFIDENTIALITY
          SECTION 18: DATA PROTECTION AND PRIVACY
          SECTION 19: PROHIBITED ACTIVITIES
          SECTION 20: SUSPENSION AND ACCOUNT TERMINATION
          SECTION 21: LIMITATION OF LIABILITY
          SECTION 22: INDEMNIFICATION
          SECTION 23: FORCE MAJEURE
          SECTION 24: DISPUTE RESOLUTION
          SECTION 25: GOVERNING LAW
          SECTION 26: AMENDMENTS
          SECTION 27: ENTIRE AGREEMENT
          SECTION 28: SIGNATURES
        - SECTION 1 should be a brief statement that this document is a Vendor Partnership
          Agreement governing the Vendor's relationship with Planura as a marketplace partner, and
          that it is separate from any Event Booking Contract between a Client and the Vendor.
        - SECTION 25 must state that this agreement is governed by the laws of the Arab Republic of
          Egypt.
        - SECTION 28 should briefly state that by signing below, Planura and the Vendor each
          acknowledge and accept the terms of this agreement, and should list placeholders for
          each party's printed name, signature and date using the representative names given to
          you (or "N/A" if none was given).
        """;

    private static string BuildVendorPartnershipUserPrompt(
        GenerateVendorPartnershipDto dto, string contractId, DateTimeOffset generatedDate, DateOnly effectiveDate) => $"""
        Draft the Vendor Partnership Agreement using only the following confirmed facts. Any fact
        listed as "N/A" was not supplied and must be written into the agreement as "N/A" - do not
        guess or fabricate a replacement value.

        Contract ID: {contractId}
        Date of Generation: {generatedDate:MMMM d, yyyy}
        Effective Date: {effectiveDate:MMMM d, yyyy}

        PLANURA (Marketplace Operator)
        Name: Planura
        Representative Name: {N(dto.PlanuraRepresentativeName)}

        VENDOR (Independent Contractor)
        Business Name: {N(dto.VendorName)}
        Representative Name: {N(dto.VendorRepresentativeName)}
        Email: {N(dto.VendorEmail)}
        Phone: {N(dto.VendorPhone)}
        Address: {N(dto.VendorAddress)}
        Service Category: {N(dto.VendorCategory)}
        City: {N(dto.VendorCity)}

        COMMISSION
        Commission Rate: {(dto.CommissionRatePercent.HasValue ? $"{dto.CommissionRatePercent.Value:0.##}%" : "N/A - refer generically to Planura's prevailing commission schedule")}

        ADDITIONAL TERMS
        {N(dto.AdditionalTerms)}

        Governing law: Arab Republic of Egypt.
        Remember: the Client is never a party to this agreement.
        """;

    // ============================================================= Shared helpers

    private async Task<string> GenerateTextAsync(
        string systemInstruction, string prompt, CancellationToken cancellationToken, int maxOutputTokens = 4096)
    {
        var text = await _geminiService.GenerateTextAsync(new GeminiTextGenerationRequest
        {
            SystemInstruction = systemInstruction,
            Prompt = prompt,
            MaxOutputTokens = maxOutputTokens
        }, cancellationToken);

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new AiProviderUnavailableExeption("The AI assistant returned an empty document. Please try again.");
        }

        return text;
    }

    private T Deserialize<T>(string json, string contractId, string stage) where T : new()
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions)
                   ?? throw new AiProviderUnavailableExeption(
                       "The AI assistant returned an empty document. Please try again.");
        }
        catch (JsonException ex)
        {
            // The response is schema-constrained, so this should not happen; if it does, the payload
            // itself is the only useful clue and belongs in the log, not in the user-facing message.
            _logger.LogError(ex,
                "Contract {ContractId}: could not parse the {Stage} response as JSON. Raw response: {RawResponse}",
                contractId, stage, Truncate(json, 4000));

            throw new AiProviderUnavailableExeption(
                "The AI assistant returned an unreadable response. Please try again.");
        }
    }

    private Task<ContractDocumentDto> RenderPdfAsync(ContractPdfModel pdfModel, string contractId, string fileName)
    {
        byte[] pdfBytes;
        try
        {
            pdfBytes = _pdfService.GenerateContractPdf(pdfModel);
        }
        catch (Exception ex) when (ex is not ApplicationException)
        {
            _logger.LogError(ex, "Failed to render document {ContractId} to PDF.", contractId);
            throw new BadRequestExeption("The document could not be rendered to PDF. Please try again.");
        }

        return Task.FromResult(new ContractDocumentDto
        {
            ContractId = contractId,
            Content = pdfBytes,
            FileName = fileName,
            ContentType = "application/pdf"
        });
    }

    private static string GenerateDocumentId(string typeCode)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I to avoid ambiguity
        Span<char> suffix = stackalloc char[6];
        for (var i = 0; i < suffix.Length; i++)
        {
            suffix[i] = alphabet[Random.Shared.Next(alphabet.Length)];
        }

        return $"PLN-{typeCode}-{DateTime.UtcNow:yyyyMMdd}-{new string(suffix)}";
    }

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max] + "…";

    private static string N(string? value) => string.IsNullOrWhiteSpace(value) ? "N/A" : value.Trim();
}
