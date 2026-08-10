using System.Linq.Expressions;
using Planura.Core.Domain.Enums;

namespace Planura.Core.Domain.Entities;

public class Payment
{
    public long Id { get; set; }
    public long BookingRequestId { get; set; }
    public long ClientId { get; set; }
    public long VendorId { get; set; }
    public decimal Amount { get; set; }

    // Deposit / partial-payment (Phase 1). Amount above is always the amount actually authorized/held
    // (the deposit on the deposit path, the full price on the full path). These record the split so
    // later phases and the vendor/admin/client UIs can show "deposit paid, remainder due" without
    // recomputing. On the full-payment path IsDeposit is false and DepositAmount is null.
    public bool IsDeposit { get; set; }
    public decimal? DepositAmount { get; set; }
    public decimal? TotalAmount { get; set; }

    // Deposit / partial-payment (Phase 2). The card saved off-session for this deposit booking and the
    // bookkeeping for the later remainder charge. All null on the full-payment path (nothing to charge).
    // RemainderGatewayReference doubles as the idempotency/audit backstop against a double-charge.
    public string? SavedPaymentMethodId { get; set; }
    public string? RemainderGatewayReference { get; set; }
    public DateTimeOffset? RemainderChargedAt { get; set; }
    public string? RemainderFailureReason { get; set; }

    // Deposit / partial-payment (Phase 3). When the off-session remainder charge failed and the grace
    // window started. The grace period ends at RemainderFailedAt + GracePeriodDays; the grace-expiry job
    // uses this. Null unless the payment is in RemainderFailed.
    public DateTimeOffset? RemainderFailedAt { get; set; }

    // Deposit / partial-payment (Phase 3). When the payment was atomically claimed into RemainderCharging.
    // The grace-expiry job reclaims a payment stuck in RemainderCharging past a timeout (e.g. an abandoned
    // on-session SCA) back to RemainderFailed, so it never stays stuck.
    public DateTimeOffset? RemainderChargingSince { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? PaymentMethod { get; set; }
    public string? GatewayReference { get; set; }
    public DateTimeOffset? AuthorizedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset? RefundedAt { get; set; }
    public string? RefundReason { get; set; }

    // The amount actually returned to the client so far, as reported by the gateway. Null until a refund is
    // issued. Kept separate from the captured amount below so a partial refund stays distinguishable from a
    // full one - before this existed, any refund of any size looked identical in the database.
    public decimal? RefundedAmount { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public BookingRequest BookingRequest { get; set; } = null!;
    public Client Client { get; set; } = null!;
    public Vendor Vendor { get; set; } = null!;

    /// <summary>
    /// The amount actually captured from the client so far, gross of refunds. Derived from this row's own
    /// captured-amount fields - never from BookingRequest.PaymentStatus, which is only a coarse cache.
    /// <para>
    /// Amount is not a reliable "paid" figure on its own: on the deposit path it holds only the deposit and
    /// is deliberately left untouched when the remainder is later charged (that is recorded via
    /// RemainderChargedAt / RemainderGatewayReference), so it stays stale at the deposit value forever.
    /// </para>
    /// <para>
    /// This is the single source of truth for every "amount collected" figure shown to clients, vendors and
    /// admins. Use this expression inside IQueryable aggregates so EF translates it to SQL; use
    /// <see cref="GetAmountCaptured"/> on entities already loaded into memory. Both run the same definition,
    /// so the two can never drift apart.
    /// </para>
    /// </summary>
    public static readonly Expression<Func<Payment, decimal>> AmountCapturedExpression = payment =>
        // Full-payment path: Amount is the whole price.
        payment.Status == PaymentStatus.Completed ? payment.Amount
        // Deposit captured, remainder still outstanding (due, mid-charge, or previously failed). A failed
        // remainder must not erase the deposit that was genuinely collected.
        : payment.Status == PaymentStatus.DepositPaid_RemainderDue
          || payment.Status == PaymentStatus.RemainderCharging
          || payment.Status == PaymentStatus.RemainderFailed ? (payment.DepositAmount ?? payment.Amount)
        // Remainder collected, so the full total is captured. Refunds are only ever issued from Completed or
        // FullyPaid, and on both of those paths TotalAmount holds the full captured price - the refund itself
        // is tracked separately in RefundedAmount, leaving this figure gross.
        : payment.Status == PaymentStatus.FullyPaid
          || payment.Status == PaymentStatus.Refunded
          || payment.Status == PaymentStatus.PartiallyRefunded ? (payment.TotalAmount ?? payment.Amount)
        // Pending / Failed / Authorized / DepositAuthorized / Cancelled: nothing has been captured.
        : 0m;

    private static readonly Func<Payment, decimal> AmountCapturedCompiled = AmountCapturedExpression.Compile();

    /// <summary>
    /// In-memory counterpart of <see cref="AmountCapturedExpression"/>. See that field for the semantics.
    /// </summary>
    public decimal GetAmountCaptured() => AmountCapturedCompiled(this);
}
