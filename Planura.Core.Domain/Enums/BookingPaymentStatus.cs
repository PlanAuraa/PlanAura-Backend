namespace Planura.Core.Domain.Enums;

public enum BookingPaymentStatus
{
    Unpaid = 1,
    Paid = 2,
    Refunded = 3,

    // Deposit / partial-payment (Phase 1). The deposit has been captured on accept but the remainder
    // is still outstanding — the booking-level counterpart of PaymentStatus.DepositPaid_RemainderDue.
    DepositPaid = 4
}
