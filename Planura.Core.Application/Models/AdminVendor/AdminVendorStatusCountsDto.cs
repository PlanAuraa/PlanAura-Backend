namespace Planura.Core.Application.Models.AdminVendor
{
    /// <summary>Summary counters shown above the "All Vendors" table, clickable as quick filters.</summary>
    public class AdminVendorStatusCountsDto
    {
        public int Unverified { get; set; }
        public int Pending { get; set; }
        public int Verified { get; set; }
        public int Trusted { get; set; }
        public int Rejected { get; set; }
    }
}
