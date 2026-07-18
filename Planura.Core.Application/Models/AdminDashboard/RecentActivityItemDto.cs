using System;

namespace Planura.Core.Application.Models.AdminDashboard
{
    /// <summary>
    /// One row in the Dashboard Overview's merged recent-activity feed - a union of
    /// VendorVerificationHistory and BookingStatusHistory, ordered by timestamp descending
    /// (AdminDashboardPlan.md Section 2.1 / Section 3 #25).
    /// </summary>
    public class RecentActivityItemDto
    {
        /// <summary>"VendorVerification" or "BookingStatus".</summary>
        public string EventType { get; set; } = null!;

        public string Description { get; set; } = null!;

        public string? ActorName { get; set; }

        public DateTimeOffset OccurredAt { get; set; }
    }
}
