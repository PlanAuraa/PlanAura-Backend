using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class ActiveVendorPackagesSpecification : BaseSpecification<VendorPackage>
{
    public ActiveVendorPackagesSpecification()
        : base(package => package.IsActive)
    {
        ApplyOrderByDescending(package => package.CreatedAt);
    }
}
