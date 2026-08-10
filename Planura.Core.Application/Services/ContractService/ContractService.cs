using System.Globalization;
using Microsoft.Extensions.Logging;
using Planura.Core.Application.Abstraction.Contract;
using Planura.Core.Application.Models;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services.Contract;

/// <summary>
/// Generates every contract type Planura issues: the Event Booking Contract (Client vs Vendor) and
/// the Vendor Partnership Agreement (Planura vs Vendor).
/// <para>
/// The two documents are generated very differently. The partnership agreement is a platform-wide
/// instrument - every vendor signs substantially the same terms - so it is drafted by Gemini from a
/// fixed clause list and boilerplate prompt (see <see cref="GenerateVendorPartnershipContractAsync"/>).
/// The booking contract governs one negotiated transaction and used to be drafted by Gemini too (a
/// two-stage extract-then-draft pipeline, validated and regenerated on a miss), but that made every
/// booking contract depend on an external, quota-limited AI service being reachable the moment a
/// client or vendor needed to read it. It is now composed deterministically from
/// <see cref="ContractGenerationContext"/> by <see cref="BuildTemplateDraft"/> instead - no AI call,
/// no quota, still specific to the transaction because every fact-bearing section is built from the
/// same context fields the AI prompt used to receive.
/// </para>
/// <para>
/// Holds no Gemini HTTP or QuestPDF layout knowledge itself - that stays in
/// <see cref="IGeminiService"/> and <see cref="IPdfService"/>.
/// </para>
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

    public Task<ContractDocumentDto> GenerateBookingContractAsync(
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

        // Composed deterministically from the platform's own resolved facts - no AI call. See
        // BuildTemplateDraft's doc comment for why: this used to be a two-to-three-call Gemini
        // pipeline (extract deal, draft, validate, regenerate on a miss), which made every contract
        // depend on an external, quota-limited service being reachable at exactly the moment a client
        // or vendor needed to read it.
        var draft = BuildTemplateDraft(context);
        var pdfModel = BuildBookingPdfModel(context, draft);

        return RenderPdfAsync(pdfModel, context.ContractId, "Contract.pdf");
    }

    public Task<ContractDocumentDto> GenerateBookingContractAsync(
        GenerateContractDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return GenerateBookingContractAsync(MapToContext(dto), cancellationToken);
    }

    /// <summary>
    /// Builds the Event Booking Contract's body sections directly from <see cref="ContractGenerationContext"/>
    /// - the exact same fact sheet the AI pipeline used to be handed - so the document is still specific
    /// to this transaction (never a generic template with names swapped in) without depending on Gemini.
    /// Boilerplate clauses that don't vary per booking (the platform's role, each party's general
    /// responsibilities, dispute resolution, governing law) are fixed text, the same way the Vendor
    /// Partnership Agreement's clause wording is fixed per vendor rather than re-invented per document.
    /// Every fact-bearing section skips itself entirely when the context has nothing to say (mirrors
    /// <see cref="ContractGenerationContext.BuildFactSheet"/>'s own rule): no section here ever prints
    /// an invented or blank value.
    /// </summary>
    private static ContractDraft BuildTemplateDraft(ContractGenerationContext context)
    {
        var sections = new List<ContractDraftSection>();

        AddIfAny(sections, BuildScopeOfServiceSection(context));
        AddIfAny(sections, BuildEventDetailsSection(context));
        AddIfAny(sections, BuildFinancialTermsSection(context));
        AddIfAny(sections, BuildClientRequirementsSection(context));
        AddIfAny(sections, BuildCancellationPolicySection(context));
        AddIfAny(sections, BuildBookingProcessSection(context));
        sections.Add(PlatformRoleSection());
        sections.Add(ResponsibilitiesSection());
        sections.Add(DisputeResolutionSection());
        sections.Add(GoverningLawSection(context));
        AddIfAny(sections, BuildOpenPointsSection(context));

        return new ContractDraft
        {
            Title = "Event Booking Contract",
            Preamble =
                "This contract sets out the terms agreed between the Client and the Vendor for the " +
                "service described below, facilitated through the Planura platform.",
            Sections = sections
        };
    }

    private static void AddIfAny(List<ContractDraftSection> sections, ContractDraftSection? section)
    {
        if (section is not null && (section.Paragraphs.Count > 0 || section.Items.Count > 0))
        {
            sections.Add(section);
        }
    }

    private static ContractDraftSection? BuildScopeOfServiceSection(ContractGenerationContext context)
    {
        var section = new ContractDraftSection { Title = "Scope of Services" };

        if (!string.IsNullOrWhiteSpace(context.Service.Category))
        {
            section.Paragraphs.Add($"The Vendor will provide services in the category of {context.Service.Category}.");
        }

        if (!string.IsNullOrWhiteSpace(context.Service.PackageTitle))
        {
            var packageLine = $"The Client has selected the Vendor's \"{context.Service.PackageTitle}\" package.";
            if (!string.IsNullOrWhiteSpace(context.Service.PackageDescription))
            {
                packageLine += $" {context.Service.PackageDescription.Trim()}";
            }
            section.Paragraphs.Add(packageLine);
        }

        if (context.Service.InclusionItems.Count > 0)
        {
            section.Items.AddRange(context.Service.InclusionItems);
        }
        else if (!string.IsNullOrWhiteSpace(context.Service.Inclusions))
        {
            section.Paragraphs.Add($"The package includes: {context.Service.Inclusions.Trim()}");
        }

        if (context.Service.MaxGuests is { } maxGuests)
        {
            section.Paragraphs.Add($"This package covers up to {maxGuests} guests.");
        }

        return section;
    }

    private static ContractDraftSection? BuildEventDetailsSection(ContractGenerationContext context)
    {
        var section = new ContractDraftSection { Title = "Event Details" };
        var booking = context.Booking;

        var eventLabel = booking.EventType ?? booking.EventTitle;
        if (!string.IsNullOrWhiteSpace(eventLabel))
        {
            section.Paragraphs.Add($"This contract concerns a {eventLabel} event.");
        }

        if (booking.EventDate is { } eventDate)
        {
            var when = eventDate.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
            if (booking.StartAt is { } start && booking.EndAt is { } end)
            {
                when +=
                    $", from {start.ToString("HH:mm 'UTC'", CultureInfo.InvariantCulture)} to " +
                    $"{end.ToString("HH:mm 'UTC'", CultureInfo.InvariantCulture)}";
            }
            section.Paragraphs.Add($"The event is scheduled for {when}.");
        }

        var location = booking.LocationDetail ?? booking.City;
        if (!string.IsNullOrWhiteSpace(location))
        {
            section.Paragraphs.Add($"The service will be performed at: {location}.");
        }

        if (booking.GuestCount is { } guests)
        {
            section.Paragraphs.Add($"The event is expected to have {guests} guests.");
        }

        if (!string.IsNullOrWhiteSpace(booking.StyleNotes))
        {
            section.Paragraphs.Add($"Style and preference notes recorded on the event plan: {booking.StyleNotes.Trim()}");
        }

        return section;
    }

    private static ContractDraftSection? BuildFinancialTermsSection(ContractGenerationContext context)
    {
        var section = new ContractDraftSection { Title = "Financial Terms" };
        var financials = context.Financials;

        if (financials.TotalAmount is { } total)
        {
            section.Paragraphs.Add(
                $"The total agreed contract amount is {Money(total)} {context.Currency}.");
        }

        if (financials.IsDepositSchedule == true)
        {
            var depositLine = "The Client pays a deposit at booking, with the remaining balance due later.";
            if (financials.DepositAmount is { } deposit)
            {
                depositLine +=
                    $" Deposit payable now: {Money(deposit)} {context.Currency}" +
                    (financials.DepositPercentage is { } pct
                        ? $" ({pct.ToString("0.##", CultureInfo.InvariantCulture)}% of the total)."
                        : ".");
            }
            section.Paragraphs.Add(depositLine);

            if (financials.RemainderAmount is { } remainder)
            {
                var remainderLine = $"Remaining balance: {Money(remainder)} {context.Currency}.";
                if (financials.RemainderDueDate is { } due)
                {
                    remainderLine += $" Due by {due.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)}.";
                }
                section.Paragraphs.Add(remainderLine);
            }
        }
        else if (financials.IsDepositSchedule == false)
        {
            section.Paragraphs.Add("The full contract amount is payable at booking.");
        }

        if (!string.IsNullOrWhiteSpace(financials.PaymentMechanism))
        {
            section.Paragraphs.Add($"Payment mechanism: {financials.PaymentMechanism.Trim()}.");
        }

        if (!string.IsNullOrWhiteSpace(financials.SettlementTerms))
        {
            section.Paragraphs.Add($"Settlement to the Vendor: {financials.SettlementTerms.Trim()}.");
        }

        return section;
    }

    private static ContractDraftSection? BuildClientRequirementsSection(ContractGenerationContext context)
    {
        if (context.ClientRequirements.Count == 0 && string.IsNullOrWhiteSpace(context.ClientNote))
        {
            return null;
        }

        var section = new ContractDraftSection { Title = "Client Requirements" };
        section.Items.AddRange(context.ClientRequirements.Where(r => !string.IsNullOrWhiteSpace(r)));

        if (!string.IsNullOrWhiteSpace(context.ClientNote))
        {
            section.Paragraphs.Add($"Additional note from the Client: {context.ClientNote.Trim()}");
        }

        return section;
    }

    private static ContractDraftSection? BuildCancellationPolicySection(ContractGenerationContext context)
    {
        var policies = context.Policies;
        if (policies.CancellationTiers.Count == 0)
        {
            return null;
        }

        var section = new ContractDraftSection { Title = "Cancellation and Refund Policy" };

        foreach (var tier in policies.CancellationTiers.OrderByDescending(t => t.MinDaysBefore))
        {
            section.Items.Add(tier.MinDaysBefore > 0
                ? $"Cancelled {tier.MinDaysBefore} or more days before the event date: " +
                  $"{tier.RefundPercent.ToString("0.##", CultureInfo.InvariantCulture)}% of the amount paid is refunded."
                : $"Cancelled fewer days before the event than any tier above: " +
                  $"{tier.RefundPercent.ToString("0.##", CultureInfo.InvariantCulture)}% of the amount paid is refunded.");
        }

        if (policies.CancellationRequiresPlatformReview)
        {
            section.Paragraphs.Add(
                "A Client request to cancel an accepted booking is reviewed by Planura before the refund is settled.");
        }

        return section;
    }

    private static ContractDraftSection? BuildBookingProcessSection(ContractGenerationContext context)
    {
        var policies = context.Policies;
        var section = new ContractDraftSection { Title = "Booking Process and Timelines" };

        if (policies.VendorResponseWindowHours is { } window)
        {
            section.Paragraphs.Add(
                $"The Vendor has {window} hours from submission to accept this booking before the reserved slot is released.");
        }

        if (context.Financials.IsDepositSchedule == true)
        {
            if (policies.RemainderChargeLeadDays is { } leadDays)
            {
                section.Paragraphs.Add($"The remaining balance is charged {leadDays} days before the event date.");
            }

            if (policies.GracePeriodDays is { } grace)
            {
                section.Paragraphs.Add($"If that charge fails, the Client has a grace period of {grace} days to resolve it.");
            }
        }

        if (policies.AutoConfirmAfterDays is { } autoConfirm)
        {
            section.Paragraphs.Add(
                $"{autoConfirm} days after the service ends, the booking is treated as completed if the Client " +
                "neither confirms delivery nor reports a problem.");
        }

        return section;
    }

    private static ContractDraftSection PlatformRoleSection() => new()
    {
        Title = "Role of the Platform",
        Paragraphs =
        {
            "Planura operates the online marketplace connecting the Client and the Vendor, and " +
            "facilitates the booking, payment processing, and contract generation for this " +
            "transaction. Planura is not itself the provider of the service described above and " +
            "does not guarantee its outcome, quality, or delivery beyond the platform mechanisms " +
            "described in this contract."
        }
    };

    private static ContractDraftSection ResponsibilitiesSection() => new()
    {
        Title = "Vendor and Client Responsibilities",
        Paragraphs =
        {
            "The Vendor is solely responsible for delivering the service described in this " +
            "contract to the standard represented on the Planura platform, including any " +
            "necessary licenses, permits, insurance, and staffing.",
            "The Client is responsible for providing accurate event details, timely access to the " +
            "event location, and any information reasonably required by the Vendor to deliver the " +
            "service."
        }
    };

    private static ContractDraftSection DisputeResolutionSection() => new()
    {
        Title = "Dispute Resolution",
        Paragraphs =
        {
            "Either party may report a problem with this booking through the Planura platform. " +
            "Planura will review the report and may adjust the booking's status, payment, or " +
            "refund in accordance with its published policies. This does not limit either party's " +
            "other legal rights."
        }
    };

    private static ContractDraftSection GoverningLawSection(ContractGenerationContext context) => new()
    {
        Title = "Governing Law",
        Paragraphs = { $"This contract is governed by the laws of {context.GoverningLaw}." }
    };

    private static ContractDraftSection? BuildOpenPointsSection(ContractGenerationContext context)
    {
        var unknowns = context.BuildUnknownFactSheet();
        if (unknowns.Count == 0)
        {
            return null;
        }

        return new ContractDraftSection
        {
            Title = "Open Points Requiring Agreement",
            Paragraphs =
            {
                "The following details were not yet recorded on the Planura platform when this " +
                "contract was generated and must be agreed between the parties directly:"
            },
            Items = unknowns.ToList()
        };
    }

    private static string Money(decimal amount) => amount.ToString("N2", CultureInfo.InvariantCulture);

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
