using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class AuthorizedPaymentByBookingRequestSpecification : BaseSpecification<Payment>
{
    public AuthorizedPaymentByBookingRequestSpecification(long bookingRequestId)
        : base(payment => payment.BookingRequestId == bookingRequestId && payment.Status == PaymentStatus.Authorized)
    {
    }
}
