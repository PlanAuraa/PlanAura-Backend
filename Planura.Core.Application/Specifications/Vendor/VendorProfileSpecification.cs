using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications.Vendor
{
    public class VendorProfileSpecification : BaseSpecification<Planura.Core.Domain.Entities.Vendor>
    {
        public VendorProfileSpecification(long vendorId)
            : base(vendor => vendor.Id == vendorId)
        {
            AddInclude(vendor => vendor.Category);
        }
    }
}
