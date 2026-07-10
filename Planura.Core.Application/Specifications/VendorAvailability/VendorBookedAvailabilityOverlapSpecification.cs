using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class VendorBookedAvailabilityOverlapSpecification : BaseSpecification<VendorAvailability>
{
    public VendorBookedAvailabilityOverlapSpecification(long vendorId, DateTimeOffset startAt, DateTimeOffset endAt)
        : base(availability =>
            availability.VendorId == vendorId &&
            availability.StartAt < endAt &&
            availability.EndAt > startAt)
    {
        ApplyOrderBy(availability => availability.StartAt);
    }
}

public class VendorAvailabilityByStatusOverlapSpecification : BaseSpecification<VendorAvailability>
{
    public VendorAvailabilityByStatusOverlapSpecification(long vendorId, DateTimeOffset startAt, DateTimeOffset endAt, AvailabilityStatus status)
        : base(availability =>
            availability.VendorId == vendorId &&
            availability.Status == status &&
            availability.StartAt < endAt &&
            availability.EndAt > startAt)
    {
        ApplyOrderBy(availability => availability.StartAt);
    }
}
