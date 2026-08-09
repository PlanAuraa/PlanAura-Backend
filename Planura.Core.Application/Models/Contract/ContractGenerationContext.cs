using System.Globalization;
using System.Text;

namespace Planura.Core.Application.Models;

/// <summary>
/// The complete, already-resolved picture of ONE real transaction between ONE client and ONE vendor,
/// assembled fresh per generation request from the database and the platform's own configured policy.
/// This is the single input to AI contract generation: if a fact is not on this object, the AI never
/// sees it, and must not write it.
/// <para>
/// Design rules that make contracts transaction-specific rather than template-shaped:
/// (a) every property is optional except the handful the platform genuinely guarantees, so a missing
///     value is represented as absent rather than as an invented placeholder;
/// (b) <see cref="BuildFactSheet"/> emits ONLY the facts that exist, so two different bookings produce
///     genuinely different prompts rather than the same prompt with different substitutions;
/// (c) <see cref="Trace"/> carries identifiers for logging only and is never sent to the model.
/// </para>
/// </summary>
public sealed class ContractGenerationContext
{
    public string ContractId { get; set; } = null!;
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>ISO currency code every monetary figure in this context is expressed in.</summary>
    public string Currency { get; set; } = "EGP";

    /// <summary>Jurisdiction whose law governs the agreement. Platform-level, not AI-invented.</summary>
    public string GoverningLaw { get; set; } = "Arab Republic of Egypt";

    public ContractPartyContext Client { get; set; } = new();
    public ContractPartyContext Vendor { get; set; } = new();
    public ContractServiceContext Service { get; set; } = new();
    public ContractBookingContext Booking { get; set; } = new();
    public ContractFinancialContext Financials { get; set; } = new();
    public ContractPolicyContext Policies { get; set; } = new();

    /// <summary>Structured, client-stated requirements for this specific booking. May be empty.</summary>
    public List<string> ClientRequirements { get; set; } = new();

    /// <summary>Free-text note the client attached to this booking. May be null.</summary>
    public string? ClientNote { get; set; }

    /// <summary>Identifiers used for logging/diagnostics only — deliberately never rendered into a prompt.</summary>
    public ContractTraceIds Trace { get; set; } = new();

    /// <summary>
    /// Renders the context as the grounded fact sheet handed to the model. Absent facts are omitted
    /// entirely rather than written as "N/A", so the model is never tempted to fill a labelled blank —
    /// facts that are contractually significant but unknown are surfaced separately by
    /// <see cref="BuildUnknownFactSheet"/> as things the parties must still agree.
    /// </summary>
    public string BuildFactSheet()
    {
        var sheet = new FactSheetWriter();

        sheet.Section("CONTRACT METADATA");
        sheet.Fact("Contract reference", ContractId);
        sheet.Fact("Date of issue", GeneratedAt.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture));
        sheet.Fact("Currency of all amounts", Currency);
        sheet.Fact("Governing law", GoverningLaw);

        sheet.Section("CLIENT (the party purchasing the service)");
        Client.Write(sheet);

        sheet.Section("VENDOR (the party providing the service)");
        Vendor.Write(sheet);

        sheet.Section("SERVICE PURCHASED");
        Service.Write(sheet);

        sheet.Section("BOOKING");
        Booking.Write(sheet);

        sheet.Section("FINANCIAL AGREEMENT");
        Financials.Write(sheet);

        sheet.Section("PLATFORM POLICIES THAT APPLY TO THIS BOOKING");
        Policies.Write(sheet);

        if (ClientRequirements.Count > 0)
        {
            sheet.Section("REQUIREMENTS STATED BY THIS CLIENT FOR THIS BOOKING");
            foreach (var requirement in ClientRequirements.Where(r => !string.IsNullOrWhiteSpace(r)))
            {
                sheet.Bullet(requirement.Trim());
            }
        }

        if (!string.IsNullOrWhiteSpace(ClientNote))
        {
            sheet.Section("NOTE SUBMITTED BY THIS CLIENT");
            sheet.Raw(ClientNote.Trim());
        }

        return sheet.ToString();
    }

    /// <summary>
    /// Contractually meaningful values the platform does not hold for this booking. Given to the model
    /// explicitly so it states them as open points requiring the parties' agreement instead of
    /// silently inventing them.
    /// </summary>
    public IReadOnlyList<string> BuildUnknownFactSheet()
    {
        var unknown = new List<string>();

        if (string.IsNullOrWhiteSpace(Booking.LocationDetail) && string.IsNullOrWhiteSpace(Booking.City))
        {
            unknown.Add("The exact venue/address where the service will be performed");
        }

        if (Booking.GuestCount is null && Service.MaxGuests is null)
        {
            unknown.Add("The number of guests or attendees the service must cover");
        }

        if (string.IsNullOrWhiteSpace(Service.Inclusions) && Service.InclusionItems.Count == 0)
        {
            unknown.Add("The itemised list of what the selected package includes");
        }

        if (Booking.StartAt is null || Booking.EndAt is null)
        {
            unknown.Add("The precise start and end time of the service on the event date");
        }

        if (Financials.RemainderAmount is > 0 && Financials.RemainderDueDate is null)
        {
            unknown.Add("The exact calendar date on which the remaining balance becomes payable");
        }

        return unknown;
    }

    /// <summary>
    /// Short, non-sensitive fingerprint of the deal used in logs to prove two generation requests
    /// really did carry different inputs. Contains no personal data.
    /// </summary>
    public string BuildDiagnosticSignature() =>
        string.Join(
            " | ",
            $"client={Trace.ClientId}",
            $"vendor={Trace.VendorId}",
            $"package={Trace.VendorPackageId?.ToString(CultureInfo.InvariantCulture) ?? "none"}",
            $"booking={Trace.BookingRequestId?.ToString(CultureInfo.InvariantCulture) ?? "preview"}",
            $"category={Service.Category ?? "unclassified"}",
            $"total={Financials.TotalAmount?.ToString("0.##", CultureInfo.InvariantCulture) ?? "unset"}{Currency}",
            $"date={Booking.EventDate?.ToString("yyyy-MM-dd") ?? "unset"}",
            $"requirements={ClientRequirements.Count}");
}

/// <summary>Identifiers for logging only. Never rendered into a prompt.</summary>
public sealed class ContractTraceIds
{
    public long? ClientId { get; set; }
    public long? VendorId { get; set; }
    public long? VendorPackageId { get; set; }
    public long? BookingRequestId { get; set; }
    public long? EventPlanId { get; set; }
    public long? AvailabilityId { get; set; }
}

/// <summary>One side of the agreement. Shared shape for the client and the vendor.</summary>
public sealed class ContractPartyContext
{
    public string? LegalName { get; set; }
    public string? RepresentativeName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }

    /// <summary>e.g. "Registered business", "Individual practitioner", "Individual client".</summary>
    public string? PartyType { get; set; }

    /// <summary>Vendor's own description of its business. Null for clients.</summary>
    public string? BusinessDescription { get; set; }

    /// <summary>Track record facts the platform holds, e.g. completed bookings. Vendor only.</summary>
    public string? TrackRecord { get; set; }

    internal void Write(FactSheetWriter sheet)
    {
        sheet.Fact("Legal name", LegalName);
        sheet.Fact("Signing representative", RepresentativeName);
        sheet.Fact("Party type", PartyType);
        sheet.Fact("Email", Email);
        sheet.Fact("Phone", Phone);
        sheet.Fact("Address", Address);
        sheet.Fact("City", City);
        sheet.Fact("Business description (vendor's own words)", BusinessDescription);
        sheet.Fact("Platform track record", TrackRecord);
    }
}

/// <summary>What is actually being bought: the vendor's category and the specific package chosen.</summary>
public sealed class ContractServiceContext
{
    /// <summary>Service category name from the platform taxonomy, e.g. "Photography", "Catering".</summary>
    public string? Category { get; set; }

    public string? PackageTitle { get; set; }
    public string? PackageDescription { get; set; }

    /// <summary>Vendor's free-text description of what the package includes, verbatim.</summary>
    public string? Inclusions { get; set; }

    /// <summary>Inclusions split into discrete lines when the vendor wrote them as a list.</summary>
    public List<string> InclusionItems { get; set; } = new();

    public int? MaxGuests { get; set; }

    internal void Write(FactSheetWriter sheet)
    {
        sheet.Fact("Service category", Category);
        sheet.Fact("Selected package", PackageTitle);
        sheet.Fact("Package description (vendor's own words)", PackageDescription);

        if (InclusionItems.Count > 0)
        {
            sheet.Label("What the package includes (vendor's own words, verbatim)");
            foreach (var item in InclusionItems)
            {
                sheet.Bullet(item);
            }
        }
        else
        {
            sheet.Fact("What the package includes (vendor's own words, verbatim)", Inclusions);
        }

        sheet.Fact("Maximum guests the package covers", MaxGuests?.ToString(CultureInfo.InvariantCulture));
    }
}

/// <summary>The when/where/how-many of this specific booking.</summary>
public sealed class ContractBookingContext
{
    public string? EventType { get; set; }
    public string? EventTitle { get; set; }
    public DateOnly? EventDate { get; set; }
    public DateTimeOffset? StartAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }
    public string? City { get; set; }

    /// <summary>More precise location than <see cref="City"/> when the platform holds one.</summary>
    public string? LocationDetail { get; set; }

    public int? GuestCount { get; set; }
    public string? StyleNotes { get; set; }
    public string? Status { get; set; }

    /// <summary>Service duration derived from the booked availability slot.</summary>
    public double? DurationHours =>
        StartAt is not null && EndAt is not null && EndAt > StartAt
            ? Math.Round((EndAt.Value - StartAt.Value).TotalHours, 2)
            : null;

    internal void Write(FactSheetWriter sheet)
    {
        sheet.Fact("Event type", EventType);
        sheet.Fact("Event plan title", EventTitle);
        sheet.Fact("Event date", EventDate?.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture));
        sheet.Fact("Service starts", StartAt?.ToString("MMMM d, yyyy 'at' HH:mm 'UTC'", CultureInfo.InvariantCulture));
        sheet.Fact("Service ends", EndAt?.ToString("MMMM d, yyyy 'at' HH:mm 'UTC'", CultureInfo.InvariantCulture));
        sheet.Fact(
            "Booked service duration",
            DurationHours is null ? null : $"{DurationHours.Value.ToString("0.##", CultureInfo.InvariantCulture)} hours");
        sheet.Fact("Location detail", LocationDetail);
        sheet.Fact("City", City);
        sheet.Fact("Guest count for this booking", GuestCount?.ToString(CultureInfo.InvariantCulture));
        sheet.Fact("Style and preference notes recorded on the event plan", StyleNotes);
        sheet.Fact("Booking status at time of issue", Status);
    }
}

/// <summary>The money, exactly as the platform will actually charge it.</summary>
public sealed class ContractFinancialContext
{
    public decimal? PackageBasePrice { get; set; }
    public decimal? TotalAmount { get; set; }

    /// <summary>True when the platform will collect a deposit now and the balance later.</summary>
    public bool? IsDepositSchedule { get; set; }

    public decimal? DepositAmount { get; set; }
    public decimal? DepositPercentage { get; set; }
    public decimal? RemainderAmount { get; set; }
    public DateOnly? RemainderDueDate { get; set; }

    /// <summary>How the payment is taken, e.g. "Card authorization held via the platform's payment processor".</summary>
    public string? PaymentMechanism { get; set; }

    /// <summary>When funds are released to the vendor, in the platform's own terms.</summary>
    public string? SettlementTerms { get; set; }

    /// <summary>The client's stated budget for the event, when recorded. Context only — never a contract term.</summary>
    public decimal? ClientBudget { get; set; }

    internal void Write(FactSheetWriter sheet)
    {
        sheet.Fact("Package list price", Money(PackageBasePrice));
        sheet.Fact("Total agreed contract amount", Money(TotalAmount));

        if (IsDepositSchedule == true)
        {
            sheet.Fact("Payment structure", "Deposit now, remaining balance later");
            sheet.Fact(
                "Deposit payable at booking",
                DepositPercentage is null
                    ? Money(DepositAmount)
                    : $"{Money(DepositAmount)} ({DepositPercentage.Value.ToString("0.##", CultureInfo.InvariantCulture)}% of the total)");
            sheet.Fact("Remaining balance", Money(RemainderAmount));
            sheet.Fact("Remaining balance due by", RemainderDueDate?.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture));
        }
        else if (IsDepositSchedule == false)
        {
            sheet.Fact("Payment structure", "Full contract amount payable at booking");
            sheet.Fact("Amount payable at booking", Money(TotalAmount));
        }

        sheet.Fact("Payment mechanism", PaymentMechanism);
        sheet.Fact("Settlement to the vendor", SettlementTerms);
        sheet.Fact("Client's recorded budget for the event (context only, not a contract term)", Money(ClientBudget));
    }

    private static string? Money(decimal? amount) =>
        amount is null ? null : amount.Value.ToString("N2", CultureInfo.InvariantCulture);
}

/// <summary>
/// Platform-enforced rules that genuinely apply to this booking, sourced from configuration rather
/// than invented. These are what make the cancellation/refund clauses match reality.
/// </summary>
public sealed class ContractPolicyContext
{
    /// <summary>Refund tiers as (days before the event, refund percentage), highest threshold first.</summary>
    public List<ContractCancellationTier> CancellationTiers { get; set; } = new();

    /// <summary>Days after the service ends before the booking auto-confirms if the client says nothing.</summary>
    public int? AutoConfirmAfterDays { get; set; }

    /// <summary>Hours the vendor has to accept before the slot hold expires.</summary>
    public int? VendorResponseWindowHours { get; set; }

    /// <summary>Days before the event the remaining balance is charged, on the deposit path.</summary>
    public int? RemainderChargeLeadDays { get; set; }

    /// <summary>Days of grace after a failed remainder charge before escalation.</summary>
    public int? GracePeriodDays { get; set; }

    /// <summary>Whether client-requested cancellation of an accepted booking is reviewed by the platform.</summary>
    public bool CancellationRequiresPlatformReview { get; set; } = true;

    internal void Write(FactSheetWriter sheet)
    {
        if (CancellationTiers.Count > 0)
        {
            sheet.Label("Cancellation refund schedule actually enforced by the platform");
            foreach (var tier in CancellationTiers.OrderByDescending(t => t.MinDaysBefore))
            {
                sheet.Bullet(tier.MinDaysBefore > 0
                    ? $"Cancelled {tier.MinDaysBefore} or more days before the event date: {tier.RefundPercent.ToString("0.##", CultureInfo.InvariantCulture)}% of the amount paid is refunded"
                    : $"Cancelled fewer days before the event than any tier above: {tier.RefundPercent.ToString("0.##", CultureInfo.InvariantCulture)}% of the amount paid is refunded");
            }
        }

        sheet.Fact(
            "Cancellation review",
            CancellationRequiresPlatformReview
                ? "A client request to cancel an accepted booking is reviewed by the platform before the refund is settled"
                : null);
        sheet.Fact(
            "Vendor acceptance window",
            VendorResponseWindowHours is null ? null : $"{VendorResponseWindowHours} hours from submission before the reserved slot is released");
        sheet.Fact(
            "Remaining balance charged",
            RemainderChargeLeadDays is null ? null : $"{RemainderChargeLeadDays} days before the event date");
        sheet.Fact(
            "Grace period after a failed balance payment",
            GracePeriodDays is null ? null : $"{GracePeriodDays} days");
        sheet.Fact(
            "Automatic completion",
            AutoConfirmAfterDays is null
                ? null
                : $"{AutoConfirmAfterDays} days after the service ends, the booking is treated as completed if the client neither confirms nor reports a problem");
    }
}

public sealed class ContractCancellationTier
{
    public int MinDaysBefore { get; set; }
    public decimal RefundPercent { get; set; }
}

/// <summary>
/// Builds the fact sheet text, skipping every absent value so the prompt contains only real data.
/// Sections that end up with no facts at all are dropped entirely.
/// </summary>
internal sealed class FactSheetWriter
{
    private readonly StringBuilder _builder = new();
    private string? _pendingSection;

    /// <summary>
    /// Number of facts/bullets/lines written under the section currently being built. Reset whenever a
    /// new section header is flushed, so callers can tell (now or in future diagnostics) whether the
    /// section in progress actually ended up with any content.
    /// </summary>
    private int _factsInSection;

    /// <summary>
    /// Queues a heading. It is only written once a fact lands underneath it, so a section with nothing
    /// to say never appears - which is what stops the fact sheet becoming a form full of blanks.
    /// </summary>
    public void Section(string title) => _pendingSection = title;

    public void Fact(string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        FlushSection();
        _builder.Append(label).Append(": ").AppendLine(value.Trim());
        _factsInSection++;
    }

    public void Label(string label)
    {
        FlushSection();
        _builder.Append(label).AppendLine(":");
        _factsInSection++;
    }

    public void Bullet(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        FlushSection();
        _builder.Append("  - ").AppendLine(value.Trim());
        _factsInSection++;
    }

    public void Raw(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        FlushSection();
        _builder.AppendLine(value.Trim());
        _factsInSection++;
    }

    private void FlushSection()
    {
        if (_pendingSection is null)
        {
            return;
        }

        if (_builder.Length > 0)
        {
            _builder.AppendLine();
        }

        _builder.Append("## ").AppendLine(_pendingSection);
        _pendingSection = null;
        _factsInSection = 0;
    }

    public override string ToString() => _builder.ToString().TrimEnd();
}
