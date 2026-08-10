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
    /// Grace window (days) after a failed remainder charge before the grace-expiry job routes the booking
    /// to admin cancellation review. Measured from Payment.RemainderFailedAt.
    /// </summary>
    public int GracePeriodDays { get; set; } = 2;

    /// <summary>
    /// How long (minutes) a payment may sit in the transient RemainderCharging claim state before the
    /// grace-expiry job reclaims it to RemainderFailed — covers an abandoned on-session SCA so a payment is
    /// never permanently stuck. Should comfortably exceed how long a client takes to complete 3-D Secure.
    /// </summary>
    public int RemainderChargingTimeoutMinutes { get; set; } = 60;

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
    /// percentages can change without a code change; always set via the "Booking:CancellationTiers"
    /// section in appsettings (see the real values there).
    /// <para>
    /// Deliberately has NO default seed values here. <c>IConfiguration</c> binds a config array onto
    /// an existing non-empty List&lt;T&gt; property by appending to it rather than replacing it, so a
    /// hardcoded default here previously meant every tier from appsettings.json was duplicated at
    /// runtime (a real client-facing bug: the rendered cancellation policy showed each refund tier
    /// twice). An empty default avoids the append entirely; if the config section is ever missing,
    /// this being empty (rather than silently duplicated) is the safer, more visible failure mode.
    /// </para>
    /// </summary>
    public List<CancellationTier> CancellationTiers { get; set; } = [];
}

public class CancellationTier
{
    public int MinDaysBefore { get; set; }
    public decimal RefundPercent { get; set; }
}
