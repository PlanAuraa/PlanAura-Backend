namespace Planura.Core.Application.Models.AdminReport
{
    /// <summary>
    /// Analytics DTOs backing a practical subset of AdminDashboardPlan.md Section 3's 26 charts -
    /// registrations trend, bookings/revenue trend, top vendors/categories, and the verification
    /// funnel. The remaining metrics either depend on the not-yet-built Reviews feature (rating
    /// distribution) or are lower-value/operational (System Health) - see the final report's
    /// Remaining Work section.
    /// </summary>
    public class MonthlyRegistrationsDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int ClientCount { get; set; }
        public int VendorCount { get; set; }
        public int TotalCount { get; set; }
    }

    public class MonthlyCountDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int Count { get; set; }
    }

    public class MonthlyAmountDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Amount { get; set; }
    }

    public class TopVendorDto
    {
        public long VendorId { get; set; }
        public string BusinessName { get; set; } = null!;
        public decimal Revenue { get; set; }
        public int BookingCount { get; set; }
    }

    public class TopCategoryDto
    {
        public long CategoryId { get; set; }
        public string CategoryName { get; set; } = null!;
        public int VendorCount { get; set; }
        public int BookingCount { get; set; }
    }

    public class VendorVerificationFunnelDto
    {
        public int Submitted { get; set; }
        public int Pending { get; set; }
        public int Approved { get; set; }
        public int Rejected { get; set; }
    }
}
