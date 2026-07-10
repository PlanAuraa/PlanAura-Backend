using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class VendorAvailabilityByIdSpecification : BaseSpecification<VendorAvailability>
{
    public VendorAvailabilityByIdSpecification(long id)
        : base(availability => availability.Id == id)
    {
        AddInclude(availability => availability.Vendor);
    }
}
