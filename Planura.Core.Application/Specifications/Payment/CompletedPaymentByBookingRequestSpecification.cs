using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

/// <summary>The captured payment to refund when an admin approves a cancellation — mirrors
/// AuthorizedPaymentByBookingRequestSpecification's shape for the post-capture (Completed) state.</summary>
public class CompletedPaymentByBookingRequestSpecification : BaseSpecification<Payment>
{
    public CompletedPaymentByBookingRequestSpecification(long bookingRequestId)
        : base(payment => payment.BookingRequestId == bookingRequestId && payment.Status == PaymentStatus.Completed)
    {
    }
}
