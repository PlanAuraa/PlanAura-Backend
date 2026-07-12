using Planura.Core.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planura.Core.Application.Specifications.VendorVerification
{
    public class VendorVerificationDetailsSpecification
    : BaseSpecification<Planura.Core.Domain.Entities.VendorVerification>
    {
        public VendorVerificationDetailsSpecification(long vendorId)
            : base(v => v.VendorId == vendorId && v.IsCurrent)
        {
            AddInclude(v => v.Vendor);

            AddInclude(v => v.Vendor.User);

            AddInclude(v => v.Vendor.Category);

            AddInclude(v => v.Documents);

            AddInclude(v => v.Vendor.PortfolioMedia);
        }
    }
}
