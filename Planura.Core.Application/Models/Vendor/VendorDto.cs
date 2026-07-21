using System;
using Planura.Core.Domain.Enums;

namespace Planura.Core.Application.Models.Vendor
{
    public class VendorDto
    {
        public long Id { get; set; }

        public string BusinessName { get; set; } = null!;

        public string? BusinessDescription { get; set; }

        public long? CategoryId { get; set; }

        public string? CategoryName { get; set; }

        public string? City { get; set; }

        public string? Address { get; set; }

        public decimal? Latitude { get; set; }

        public decimal? Longitude { get; set; }

        public string? CoverImageUrl { get; set; }

        public string? LogoUrl { get; set; }

        public string VerificationStatus { get; set; } = null!;

        public VendorType VendorType { get; set; }

        public decimal AvgRating { get; set; }

        public int TotalReviews { get; set; }

        public int TotalCompletedBookings { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public bool HasPartnershipAgreement => !string.IsNullOrWhiteSpace(PartnershipAgreementUrl);
        public string? PartnershipAgreementId { get; set; }
        public string? PartnershipAgreementUrl { get; set; }
        public DateTimeOffset? PartnershipAgreementGeneratedAt { get; set; }
    }
}
