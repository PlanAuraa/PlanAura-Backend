using Planura.Core.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;

namespace Planura.Core.Application.Specifications.AdminBooking
{
    /// <summary>
    /// Lists bookings with a dispute. <paramref name="status"/> null = every booking that has ever
    /// been disputed (open or resolved); otherwise filters to that exact dispute status. Replaces the
    /// previous hard-coded "Open only" behavior so resolved disputes remain visible/auditable
    /// afterward (AdminDashboardPlan.md Section 2.7 / the review's Bug #4).
    /// </summary>
    public class OpenDisputesSpecification : BaseSpecification<BookingRequest>
    {
        public OpenDisputesSpecification(DisputeStatus? status = DisputeStatus.Open)
        : base(status is null
            ? d => d.DisputeStatus != null
            : d => d.DisputeStatus == status)
        {
            AddInclude(d => d.Client.User);
            AddInclude(d => d.Vendor.User);

            ApplyOrderByDescending(d => d.DisputedAt);
        }
    }
}
