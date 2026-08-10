using Planura.Core.Domain.Enums;

namespace Planura.Core.Domain.Entities;

public class Vendor
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string BusinessName { get; set; } = null!;
    public string? BusinessDescription { get; set; }
    public long? CategoryId { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? LogoUrl { get; set; }
    public string VerificationStatus { get; set; } = "unverified";
    public decimal AvgRating { get; set; }
    public int TotalReviews { get; set; }
    public int TotalCompletedBookings { get; set; }
    public VendorType VendorType { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // AI-generated Vendor Partnership Agreement with Planura. Generated at most once per vendor -
    // a non-null PartnershipAgreementUrl means "already has one, never generate another".
    public string? PartnershipAgreementId { get; set; }
    public string? PartnershipAgreementUrl { get; set; }
    public DateTimeOffset? PartnershipAgreementGeneratedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public ServiceCategory? Category { get; set; }
    public ICollection<VendorVerification> Verifications { get; set; } = new List<VendorVerification>();
    public ICollection<VendorPackage> Packages { get; set; } = new List<VendorPackage>();
    public ICollection<VendorAvailability> Availability { get; set; } = new List<VendorAvailability>();
    public ICollection<PortfolioMedia> PortfolioMedia { get; set; } = new List<PortfolioMedia>();
    public ICollection<PortfolioLink> PortfolioLinks { get; set; } = new List<PortfolioLink>();
    public ICollection<EventPlanItem> EventPlanItems { get; set; } = new List<EventPlanItem>();
    public ICollection<BookingRequest> BookingRequests { get; set; } = new List<BookingRequest>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<ReviewResponse> ReviewResponses { get; set; } = new List<ReviewResponse>();
    public ICollection<VendorPayout> Payouts { get; set; } = new List<VendorPayout>();
}
