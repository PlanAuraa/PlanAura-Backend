using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications.Vendor
{
    public class PortfolioLinksByVendorSpecification : BaseSpecification<PortfolioLink>
    {
        public PortfolioLinksByVendorSpecification(long vendorId)
            : base(link => link.VendorId == vendorId)
        {
            ApplyOrderBy(link => link.CreatedAt);
        }
    }
}
