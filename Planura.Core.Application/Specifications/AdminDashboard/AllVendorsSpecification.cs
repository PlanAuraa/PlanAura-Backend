using Planura.Core.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planura.Core.Application.Specifications.AdminDashboard
{
    public class AllVendorsSpecification : BaseSpecification<Planura.Core.Domain.Entities.Vendor>
    {
        public AllVendorsSpecification()
            : base(v => true)
        {
        }
    }
}
