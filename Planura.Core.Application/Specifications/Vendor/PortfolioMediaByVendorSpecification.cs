using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications.Vendor
{
    public class PortfolioMediaByVendorSpecification : BaseSpecification<PortfolioMedia>
    {
        public PortfolioMediaByVendorSpecification(long vendorId)
            : base(media => media.VendorId == vendorId)
        {
            ApplyOrderBy(media => media.DisplayOrder);
        }
    }
}
