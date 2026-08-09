namespace Planura.Core.Domain.Enums;

public enum PaymentStatus
{
    Pending = 1,
    Completed = 2,
    Failed = 3,
    Refunded = 4,
    Authorized = 5,
    Cancelled = 6,

    // Deposit / partial-payment (Phase 1). Set when a deposit-path booking is accepted and only the
    // deposit has been captured — the remainder is outstanding and, in Phase 1, is not yet collected by
    // any mechanism. This is the resting state for accepted deposit-path bookings.
    DepositPaid_RemainderDue = 7,

    // Deposit / partial-payment (Phase 1). The deposit-path counterpart of Authorized: only the deposit
    // is held on the client's card while the request awaits the vendor's accept/reject. Voided the same
    // way a full Authorized hold is on reject/cancel/expiry.
    DepositAuthorized = 8,

    // Deposit / partial-payment (Phase 2). The remainder was charged off-session successfully — the
    // deposit booking is now paid in full. Terminal success state for the deposit path.
    FullyPaid = 9,

    // Deposit / partial-payment (Phase 2). The off-session remainder charge failed (insufficient funds,
    // card declined, SCA/authentication_required — all treated the same). The booking rests here; the job
    // does NOT retry it. Notification + grace-period + eventual resolution come in Phase 3.
    RemainderFailed = 10,

    // Deposit / partial-payment (Phase 3). Transient claim state: exactly one actor (the background job OR
    // the client's on-session pay-remainder) atomically transitions DepositPaid_RemainderDue/RemainderFailed
    // into this before charging, so the two can never double-charge. Resolves to FullyPaid on success or
    // back to RemainderFailed on failure. A payment left here (e.g. abandoned SCA) awaits reconciliation.
    RemainderCharging = 11
}
