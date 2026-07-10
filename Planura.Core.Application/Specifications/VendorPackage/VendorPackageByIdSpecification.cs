using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class VendorPackageByIdSpecification : BaseSpecification<VendorPackage>
{
    public VendorPackageByIdSpecification(long id)
        : base(package => package.Id == id)
    {
        AddInclude(package => package.Vendor);
    }
}
