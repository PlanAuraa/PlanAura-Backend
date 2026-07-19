using Planura.Core.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planura.Core.Application.Models.VendorVerification
{
    public class VendorDetailsDto
    {
        public long VendorId { get; set; }

        /// <summary>The underlying ApplicationUser id - needed to call the existing
        /// suspend/reactivate endpoints (api/admin/users/{userId}/...) from this page.</summary>
        public long UserId { get; set; }

        public string VendorName { get; set; } = null!;

        public string? Email { get; set; }

        public string? PhoneNumber { get; set; }

        public string BusinessName { get; set; } = null!;

        public string? BusinessDescription { get; set; }

        public VendorType VendorType { get; set; }

        public string? CategoryName { get; set; }

        public string? City { get; set; }

        public string? Address { get; set; }

        public string VerificationStatus { get; set; } = null!;

        public DateTimeOffset? SubmittedAt { get; set; }

        public DateTimeOffset? ReviewedAt { get; set; }

        public string? RejectionReason { get; set; }

        /// <summary>Set once a Verified vendor has been promoted to Trusted (POST .../trust).</summary>
        public DateTimeOffset? TrustedSince { get; set; }

        // Performance snapshot, denormalized on the Vendor entity itself - not verification data,
        // but useful context for an admin reviewing this vendor in one place.
        public decimal AvgRating { get; set; }
        public int TotalReviews { get; set; }
        public int TotalCompletedBookings { get; set; }

        public List<VendorDocumentDto> Documents { get; set; } = [];

        public List<PortfolioMediaDto> PortfolioMedia { get; set; } = [];
    }
}
