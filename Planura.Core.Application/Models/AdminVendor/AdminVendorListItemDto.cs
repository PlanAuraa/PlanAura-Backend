using System;
using Planura.Core.Domain.Enums;

namespace Planura.Core.Application.Models.AdminVendor
{
    /// <summary>
    /// Row shape for the "All Vendors" admin list (AdminDashboardPlan.md 2.4) - unlike the public
    /// VendorController.BrowseVendors (ClientOnly, hard-filtered to Verified/Trusted), this surfaces
    /// vendors in every verification status plus the account-active flag.
    /// </summary>
    public class AdminVendorListItemDto
    {
        public long VendorId { get; set; }
        public long UserId { get; set; }
        public string VendorName { get; set; } = null!;
        public string BusinessName { get; set; } = null!;
        public VendorType VendorType { get; set; }
        public string? CategoryName { get; set; }
        public string? City { get; set; }
        public string VerificationStatus { get; set; } = null!;
        public bool IsAccountActive { get; set; }
        public decimal AvgRating { get; set; }
        public int TotalReviews { get; set; }
        public int TotalCompletedBookings { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
