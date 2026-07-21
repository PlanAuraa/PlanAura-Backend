using Microsoft.Extensions.Logging;
using Planura.Core.Application.Abstraction.Contract;
using Planura.Core.Application.Models;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services.Contract;

/// <summary>
/// Orchestrates AI document generation for every contract type Planura issues: the Event Booking
/// Contract (Client vs Vendor) and the Vendor Partnership Agreement (Planura vs Vendor). Each flow
/// builds its own legal prompt and its own <see cref="ContractPdfModel"/>, then shares the same
/// Gemini call and the same QuestPDF template - so every document stays visually and structurally
/// consistent while the legal content stays type-specific. Holds no Gemini HTTP or QuestPDF layout
/// knowledge itself - that stays in <see cref="IGeminiService"/> and <see cref="IPdfService"/>.
/// </summary>
public class ContractService : IContractService
{
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

    public async Task<ContractDocumentDto> GenerateBookingContractAsync(GenerateContractDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.EventDate < DateOnly.FromDateTime(DateTime.UtcNow.Date.AddYears(-1)))
        {
            throw new BadRequestExeption("Event date is not valid.");
        }

        var contractId = GenerateDocumentId("CN");
        var generatedDate = DateTimeOffset.UtcNow;
        var currency = string.IsNullOrWhiteSpace(dto.Currency) ? "EGP" : dto.Currency.ToUpperInvariant();

        _logger.LogInformation(
            "Generating booking contract {ContractId} for client {ClientName} / vendor {VendorName}.",
            contractId, dto.ClientName, dto.VendorName);

        var contractBody = await GenerateTextAsync(
            BuildBookingContractSystemPrompt(),
            BuildBookingContractUserPrompt(dto, contractId, generatedDate, currency),
            cancellationToken);

        var pdfModel = new ContractPdfModel
        {
            ContractId = contractId,
            GeneratedDate = generatedDate,
            DocumentTitle = "Event Booking Contract",
            DocumentTagline = "A binding agreement between the Client and the Vendor, facilitated by Planura.",
            IntroParagraph =
                "This contract sets out the terms agreed between the Client and the Vendor for the event " +
                "described below, facilitated through the Planura platform.",
            ContractBody = contractBody,
            PartyA = new ContractPartyDto
            {
                Label = "CLIENT",
                Name = dto.ClientName,
                Email = dto.ClientEmail,
                Phone = dto.ClientPhone,
                Address = dto.ClientAddress,
                RepresentativeName = dto.ClientRepresentativeName
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
                new("Event Type", dto.EventType),
                new("Event Date", dto.EventDate.ToString("MMMM d, yyyy")),
                new("Location", N(dto.EventLocation)),
                new("Guest Count", dto.GuestCount.HasValue ? dto.GuestCount.Value.ToString() : "N/A"),
                new("Agreed Price", $"{dto.Price:N2} {currency}"),
                new("Contract ID", contractId)
            }
        };

        return await RenderPdfAsync(pdfModel, contractId, "Contract.pdf");
    }

    private static string BuildBookingContractSystemPrompt() => """
        You are a senior contracts lawyer drafting a formal Event Booking Contract for Planura,
        an event-planning platform that connects Clients with service Vendors (catering, venues,
        photography, decor, entertainment, and similar categories).

        Write in professional, precise legal English suitable for a real, enforceable contract.

        ABSOLUTE RULES - DO NOT BREAK THESE:
        1. Never invent or assume any fact that was not explicitly provided to you: no invented
           addresses, emails, phone numbers, representative names, dates, or amounts. If a fact
           was provided to you as "N/A", write it into the contract exactly as "N/A" - never
           replace "N/A" with a plausible-sounding guess.
        2. The governing law of this contract is always the Arab Republic of Egypt, regardless of
           where the parties are located.
        3. Output ONLY the contract text itself. Do not include any preamble, explanation,
           apology, or commentary before or after the contract. Do not use markdown formatting,
           code fences, asterisks, or bullet characters of any kind.

        REQUIRED STRUCTURE AND FORMATTING:
        - Begin with a short introductory paragraph naming the parties (Client and Vendor) and
          stating that Planura acts as the facilitating platform for this booking.
        - Divide the body into major sections. Each section MUST start on its own line using
          exactly this format: "SECTION <n>: <TITLE IN UPPERCASE>" (for example
          "SECTION 1: PARTIES TO THE AGREEMENT"), followed by a blank line and then the section's
          plain-text paragraphs. Do not number sections any other way.
        - Within a section, if a numbered or lettered list is genuinely useful, format each list
          line starting with "(a)", "(b)", "(c)" etc. on its own line - never use markdown bullets
          or asterisks.
        - Include, in this order, at minimum the following sections: Parties to the Agreement;
          Event and Booking Details; Scope of Services; Payment Terms; Planura's Responsibilities
          (Planura's role as facilitator/platform, not as the service provider); Vendor's
          Responsibilities; Client's Responsibilities; Cancellation and Rescheduling; Liability
          Limitation; Force Majeure; Confidentiality; Amendments; Entire Agreement; Governing Law
          (Egypt); and Signatures.
        - The final "SECTION: SIGNATURES" section should briefly state that by signing below, the
          Client and the Vendor acknowledge and accept the terms of this contract, and should list
          placeholders for the Client's and Vendor's printed name, signature and date using the
          representative names given to you (or "N/A" if none was given).
        """;

    private static string BuildBookingContractUserPrompt(GenerateContractDto dto, string contractId, DateTimeOffset generatedDate, string currency) => $"""
        Draft the Event Booking Contract using only the following confirmed facts. Any fact
        listed as "N/A" was not supplied and must be written into the contract as "N/A" - do not
        guess or fabricate a replacement value.

        Contract ID: {contractId}
        Date of Generation: {generatedDate:MMMM d, yyyy}

        CLIENT
        Name: {N(dto.ClientName)}
        Representative Name: {N(dto.ClientRepresentativeName)}
        Email: {N(dto.ClientEmail)}
        Phone: {N(dto.ClientPhone)}
        Address: {N(dto.ClientAddress)}

        VENDOR
        Name: {N(dto.VendorName)}
        Representative Name: {N(dto.VendorRepresentativeName)}
        Email: {N(dto.VendorEmail)}
        Phone: {N(dto.VendorPhone)}
        Address: {N(dto.VendorAddress)}

        EVENT
        Event Type: {N(dto.EventType)}
        Event Date: {dto.EventDate:MMMM d, yyyy}
        Event Location: {N(dto.EventLocation)}
        Guest Count: {(dto.GuestCount.HasValue ? dto.GuestCount.Value.ToString() : "N/A")}

        PAYMENT
        Agreed Price: {dto.Price.ToString("N2")} {currency}

        ADDITIONAL TERMS REQUESTED BY THE CLIENT OR VENDOR
        {N(dto.AdditionalTerms)}

        Governing law: Arab Republic of Egypt.
        """;

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

    private static string N(string? value) => string.IsNullOrWhiteSpace(value) ? "N/A" : value.Trim();
}
