using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class VendorPackagesByVendorSpecification : BaseSpecification<VendorPackage>
{
    public VendorPackagesByVendorSpecification(long vendorId, bool activeOnly = false)
        : base(package => package.VendorId == vendorId && (!activeOnly || package.IsActive))
    {
        ApplyOrderByDescending(package => package.CreatedAt);
    }
}
