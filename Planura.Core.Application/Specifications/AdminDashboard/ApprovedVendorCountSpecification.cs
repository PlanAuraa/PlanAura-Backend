using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planura.Core.Application.Specifications.AdminDashboard
{
    public class ApprovedVendorCountSpecification
    :
 BaseSpecification<Planura.Core.Domain.Entities.VendorVerification>
    {
        public ApprovedVendorCountSpecification()
            : base(v =>
    v.IsCurrent &&
    (
        v.Status == VerificationStatus.Verified ||
        v.Status == VerificationStatus.Trusted
    ))
        { }

    }

}