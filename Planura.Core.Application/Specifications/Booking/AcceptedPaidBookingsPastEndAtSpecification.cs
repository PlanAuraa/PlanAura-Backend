using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class AcceptedPaidBookingsPastEndAtSpecification : BaseSpecification<BookingRequest>
{
    public AcceptedPaidBookingsPastEndAtSpecification(DateTimeOffset now)
        : base(booking =>
            booking.Status == BookingStatus.Accepted &&
            booking.PaymentStatus == BookingPaymentStatus.Paid &&
            booking.VendorAvailability.Any(availability => availability.EndAt < now))
    {
    }
}
