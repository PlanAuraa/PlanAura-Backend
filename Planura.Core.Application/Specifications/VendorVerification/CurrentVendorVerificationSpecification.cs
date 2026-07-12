using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications.VendorVerification;

public class CurrentVendorVerificationSpecification
    : BaseSpecification<Planura.Core.Domain.Entities.VendorVerification>
{
    public CurrentVendorVerificationSpecification(long vendorId)
        : base(v => v.VendorId == vendorId && v.IsCurrent)
    {
    }
}