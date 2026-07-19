using Planura.Core.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planura.Core.Application.Specifications.AdminDashboard
{
    public class AllClientsSpecification : BaseSpecification<Planura.Core.Domain.Entities.Client>
    {
        public AllClientsSpecification()
            : base(c => true)
        {
        }
       
    }
}
