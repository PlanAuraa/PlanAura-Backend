namespace Planura.Core.Application.Common;

public class BookingOptions
{
    public const string SectionName = "Booking";

    public int HoldTtlHours { get; set; } = 48;

    // ---------------- Deposit / partial-payment (Phase 1) ----------------

    /// <summary>
    /// Percentage of the package price collected up front as a deposit when a booking is far enough
    /// out to qualify for the deposit path (see <see cref="FullPaymentThresholdDays"/>). The remainder
    /// is collected later (later phases). Applied server-side to package.BasePrice only.
    /// </summary>
    public decimal DepositPercentage { get; set; } = 20m;

    /// <summary>
    /// If the event is this many days away or fewer at booking time, the full price is authorized
    /// (today's behavior). Further out than this, only the deposit is authorized. Inclusive: an event
    /// exactly this many days away still takes the full-payment path.
    /// </summary>
    public int FullPaymentThresholdDays { get; set; } = 7;

    /// <summary>
    /// How many days before the event the remainder should be charged. Stored now so the config is
    /// complete; NOT used in Phase 1 (no remainder-charge mechanism exists yet — wired in a later phase).
    /// </summary>
    public int RemainderChargeLeadDays { get; set; } = 4;

    /// <summary>
    /// Grace window after a failed remainder charge before escalation/cancellation. Stored now so the
    /// config is complete; NOT used in Phase 1 (wired in a later phase).
    /// </summary>
    public int GracePeriodDays { get; set; } = 2;

    /// <summary>
    /// Days after a booking enters AwaitingConfirmation (the event's slot has ended) before it
    /// auto-confirms to Completed if the client neither confirms nor reports a problem. Set to 0
    /// or negative to disable auto-confirm entirely (booking then waits indefinitely).
    /// </summary>
    public int AutoConfirmAfterDays { get; set; } = 7;

    /// <summary>
    /// Cancellation refund tiers, evaluated by days remaining before EventDate at the moment the
    /// client requests cancellation. The tier with the highest MinDaysBefore that the booking still
    /// satisfies applies (see BookingService.ResolveCancellationRefund) — so tiers should be listed
    /// with MinDaysBefore descending, ending in a 0-day catch-all. Configurable here so refund
    /// percentages can change without a code change.
    /// </summary>
    public List<CancellationTier> CancellationTiers { get; set; } =
    [
        new() { MinDaysBefore = 30, RefundPercent = 100 },
        new() { MinDaysBefore = 14, RefundPercent = 50 },
        new() { MinDaysBefore = 7, RefundPercent = 25 },
        new() { MinDaysBefore = 0, RefundPercent = 0 }
    ];
}

public class CancellationTier
{
    public int MinDaysBefore { get; set; }
    public decimal RefundPercent { get; set; }
}
