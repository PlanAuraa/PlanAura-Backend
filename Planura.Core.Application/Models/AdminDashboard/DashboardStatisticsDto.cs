using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planura.Core.Application.Models.AdminDashboard
{
    public class DashboardStatisticsDto
    {
        public int TotalVendors { get; set; }
        public int PendingVendors { get; set; }
        public int ApprovedVendors { get; set; }
        public int RejectedVendors { get; set; }

        /// <summary>TotalVendors - (Pending + Approved + Rejected). Present so the four vendor
        /// counters reconcile on a KPI card - see AdminDashboardImplementationReview.md, Bugs table
        /// (PendingVendors + ApprovedVendors + RejectedVendors previously didn't equal TotalVendors).</summary>
        public int UnverifiedVendors { get; set; }

        public int TotalClients { get; set; }
        public int TotalBookingRequests { get; set; }
        public decimal TotalRevenue { get; set; }

        /// <summary>Bookings with DisputeStatus = Open right now - feeds the "Disputes" sidebar badge.</summary>
        public int OpenDisputes { get; set; }

        public int NewClientsThisWeek { get; set; }
        public int NewVendorsThisWeek { get; set; }

        /// <summary>Users with LastLoginAt within the last 30 days.</summary>
        public int ActiveUsersLast30Days { get; set; }

        /// <summary>Sum of Completed payments captured in the current calendar month.</summary>
        public decimal RevenueThisMonth { get; set; }

        /// <summary>BookingStatus enum name -> count, for the status-breakdown donut chart.</summary>
        public Dictionary<string, int> BookingsByStatus { get; set; } = new();
    }
}
