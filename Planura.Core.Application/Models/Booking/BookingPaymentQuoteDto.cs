namespace Planura.Core.Application.Models;

/// <summary>
/// What the client will actually be charged for a booking they have not yet submitted, resolved
/// server-side from the package price and the platform's deposit rules.
/// <para>
/// This exists so the checkout UI can state "total / due now / remaining" without reimplementing
/// <c>BookingService.ResolvePaymentPlan</c> in TypeScript. The backend stays the single source of
/// truth for every figure here; the frontend only formats them.
/// </para>
/// </summary>
public class BookingPaymentQuoteDto
{
    /// <summary>ISO currency code all amounts below are expressed in.</summary>
    public string Currency { get; set; } = "EGP";

    /// <summary>Full contract value — the package's price. Always the total, on either payment path.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>What will be authorized on the card at submission: the deposit, or the full total.</summary>
    public decimal AmountDueNow { get; set; }

    /// <summary>Total less the amount due now. Zero on the full-payment path.</summary>
    public decimal RemainingAmount { get; set; }

    /// <summary>True when this booking takes the deposit path rather than paying in full up front.</summary>
    public bool IsDeposit { get; set; }

    /// <summary>The configured deposit percentage, present only on the deposit path.</summary>
    public decimal? DepositPercentage { get; set; }

    /// <summary>
    /// Days between now and the event. The deposit path applies beyond
    /// <see cref="FullPaymentThresholdDays"/>; at or under it, the full amount is taken.
    /// </summary>
    public int DaysUntilEvent { get; set; }

    /// <summary>The threshold that decided the payment path, so the UI can explain the rule honestly.</summary>
    public int FullPaymentThresholdDays { get; set; }

    /// <summary>
    /// Hours the vendor has to accept before the reserved slot is released and the card hold is
    /// voided. Drives the "what happens next" messaging.
    /// </summary>
    public int VendorResponseWindowHours { get; set; }

    /// <summary>
    /// Whether the platform will collect <see cref="RemainingAmount"/> automatically. Currently always
    /// false: the deposit split is implemented but no remainder-collection mechanism exists yet, so the
    /// UI must present the balance as arranged separately rather than as a scheduled charge.
    /// </summary>
    public bool RemainderCollectionScheduled { get; set; }
}
