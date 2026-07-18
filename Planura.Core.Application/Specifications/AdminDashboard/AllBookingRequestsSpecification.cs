using Planura.Core.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planura.Core.Application.Specifications.AdminDashboard
{
    public class AllBookingRequestsSpecification : BaseSpecification<Planura.Core.Domain.Entities.BookingRequest>
    {
        public AllBookingRequestsSpecification()
            : base(br => true)
        {
        }
    }
}
