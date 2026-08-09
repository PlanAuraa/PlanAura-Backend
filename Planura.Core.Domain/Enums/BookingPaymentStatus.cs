namespace Planura.Core.Domain.Enums;

public enum BookingPaymentStatus
{
    Unpaid = 1,
    Paid = 2,
    Refunded = 3,

    // Deposit / partial-payment (Phase 1). The deposit has been captured on accept but the remainder
    // is still outstanding — the booking-level counterpart of PaymentStatus.DepositPaid_RemainderDue.
    DepositPaid = 4,

    // Deposit / partial-payment (Phase 2). The off-session remainder charge failed — booking-level
    // counterpart of PaymentStatus.RemainderFailed. Excludes the booking from the remainder-charge job so
    // it is not retried; Phase 3 handles notification, grace, and resolution.
    RemainderFailed = 5
}
