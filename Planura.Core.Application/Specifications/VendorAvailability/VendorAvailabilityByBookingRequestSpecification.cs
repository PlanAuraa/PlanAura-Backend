using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class VendorAvailabilityByBookingRequestSpecification : BaseSpecification<VendorAvailability>
{
    public VendorAvailabilityByBookingRequestSpecification(long bookingRequestId)
        : base(availability => availability.BookingRequestId == bookingRequestId)
    {
    }
}
