namespace Planura.Core.Application.Common;

public class BookingOptions
{
    public const string SectionName = "Booking";

    public int HoldTtlHours { get; set; } = 48;

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
