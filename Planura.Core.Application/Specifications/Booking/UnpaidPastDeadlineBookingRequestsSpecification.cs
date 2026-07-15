using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class UnpaidPastDeadlineBookingRequestsSpecification : BaseSpecification<BookingRequest>
{
    public UnpaidPastDeadlineBookingRequestsSpecification(DateTimeOffset cutoff)
        : base(booking =>
            booking.Status == BookingStatus.Accepted &&
            booking.PaymentStatus == BookingPaymentStatus.Unpaid &&
            booking.RespondedAt != null &&
            booking.RespondedAt < cutoff)
    {
    }
}
