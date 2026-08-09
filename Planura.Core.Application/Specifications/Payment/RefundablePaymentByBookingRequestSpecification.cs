using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

/// <summary>
/// The fully-captured payment for a booking that an admin can refund on cancellation: Completed (the
/// full-payment path) or FullyPaid (the deposit path once its remainder was collected). Superset of
/// CompletedPaymentByBookingRequestSpecification, adding the deposit case; deposit-only bookings (still
/// owing a remainder) have no refundable payment and are non-refundable by policy.
/// </summary>
public class RefundablePaymentByBookingRequestSpecification : BaseSpecification<Payment>
{
    public RefundablePaymentByBookingRequestSpecification(long bookingRequestId)
        : base(payment => payment.BookingRequestId == bookingRequestId
            && (payment.Status == PaymentStatus.Completed || payment.Status == PaymentStatus.FullyPaid))
    {
    }
}
