using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class VendorByUserIdSpecification : BaseSpecification<Planura.Core.Domain.Entities.Vendor>
{
    public VendorByUserIdSpecification(long userId)
        : base(vendor => vendor.UserId == userId)
    {
    }
}
