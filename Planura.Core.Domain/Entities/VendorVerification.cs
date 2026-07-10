namespace Planura.Core.Domain.Entities;

public class VendorVerification
{
    public long Id { get; set; }
    public long VendorId { get; set; }
    public string Status { get; set; } = "unverified";
    public string? CommercialDocUrl { get; set; }
    public string? NationalIdDocUrl { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public long? ReviewedByAdminId { get; set; }
    public string? RejectionReason { get; set; }
    public DateTimeOffset? TrustedSince { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Vendor Vendor { get; set; } = null!;
    public ApplicationUser? ReviewedByAdmin { get; set; }
    public ICollection<VendorVerificationHistory> History { get; set; } = new List<VendorVerificationHistory>();
}
