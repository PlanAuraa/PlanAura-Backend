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
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public BookingRequest BookingRequest { get; set; } = null!;
    public Client Client { get; set; } = null!;
    public Vendor Vendor { get; set; } = null!;
}
