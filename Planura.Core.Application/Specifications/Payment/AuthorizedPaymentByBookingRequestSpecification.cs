using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class AuthorizedPaymentByBookingRequestSpecification : BaseSpecification<Payment>
{
    public AuthorizedPaymentByBookingRequestSpecification(long bookingRequestId)
        // Matches both the full-payment hold (Authorized) and the deposit hold (DepositAuthorized): both
        // represent an uncaptured authorization awaiting the vendor, and Accept/Reject/Cancel/hold-expiry
        // all locate the held payment through this spec.
        : base(payment => payment.BookingRequestId == bookingRequestId
            && (payment.Status == PaymentStatus.Authorized || payment.Status == PaymentStatus.DepositAuthorized))
    {
    }
}
