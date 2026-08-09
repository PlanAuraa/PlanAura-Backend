using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

/// <summary>
/// The deposit Payment for a booking that is still eligible for a remainder charge — resting at
/// DepositPaid_RemainderDue. Once the remainder succeeds (FullyPaid) or fails (RemainderFailed), the
/// payment leaves this state and is no longer selected, which is the primary no-double-charge guard.
/// </summary>
public class RemainderChargeablePaymentByBookingSpecification : BaseSpecification<Payment>
{
    public RemainderChargeablePaymentByBookingSpecification(long bookingRequestId)
        : base(payment => payment.BookingRequestId == bookingRequestId
            && payment.Status == PaymentStatus.DepositPaid_RemainderDue)
    {
    }
}
