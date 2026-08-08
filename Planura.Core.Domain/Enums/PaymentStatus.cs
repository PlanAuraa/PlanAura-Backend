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
    DepositAuthorized = 8
}
