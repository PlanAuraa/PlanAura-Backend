using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class HeldExpiredVendorAvailabilitySpecification : BaseSpecification<VendorAvailability>
{
    public HeldExpiredVendorAvailabilitySpecification(DateTimeOffset now)
        : base(availability =>
            availability.Status == AvailabilityStatus.Held &&
            availability.HoldExpiresAt != null &&
            availability.HoldExpiresAt < now)
    {
        AddInclude(availability => availability.BookingRequest!);
    }
}
