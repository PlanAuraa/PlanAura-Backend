namespace Planura.Core.Domain.Entities;

public class VendorVerification
{
    public long Id { get; set; }
    public long VendorId { get; set; }
    public string Status { get; set; } = "pending";
    public bool IsCurrent { get; set; } = true;
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

    public ICollection<VendorVerificationDocument> Documents { get; set; }
    = new List<VendorVerificationDocument>();
}
