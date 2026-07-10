using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class VendorAvailabilityByVendorSpecification : BaseSpecification<VendorAvailability>
{
    public VendorAvailabilityByVendorSpecification(long vendorId)
        : base(availability => availability.VendorId == vendorId)
    {
        ApplyOrderBy(availability => availability.StartAt);
    }
}
