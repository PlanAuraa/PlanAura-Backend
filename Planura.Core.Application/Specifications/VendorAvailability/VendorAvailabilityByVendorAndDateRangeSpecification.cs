using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class VendorAvailabilityByVendorAndDateRangeSpecification : BaseSpecification<VendorAvailability>
{
    public VendorAvailabilityByVendorAndDateRangeSpecification(long vendorId, DateTimeOffset startAt, DateTimeOffset endAt)
        : base(availability =>
            availability.VendorId == vendorId &&
            availability.StartAt < endAt &&
            availability.EndAt > startAt)
    {
        ApplyOrderBy(availability => availability.StartAt);
    }
}
