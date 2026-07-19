using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Repositories;
using Planura.Core.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planura.Core.Application.Specifications.AdminDashboard
{
    public class RejectedVendorCountSpecification
    :
 BaseSpecification<Planura.Core.Domain.Entities.VendorVerification>
    {
        public RejectedVendorCountSpecification()
            : base(v =>
                v.IsCurrent &&
                v.Status == VerificationStatus.Rejected)
        { }
    }


}