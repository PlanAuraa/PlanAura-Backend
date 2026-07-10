namespace Planura.Core.Domain.Entities;

public class VendorVerificationHistory
{
    public long Id { get; set; }
    public long VendorVerificationId { get; set; }
    public string? PreviousStatus { get; set; }
    public string NewStatus { get; set; } = null!;
    public long? ChangedByAdminId { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;

    public VendorVerification VendorVerification { get; set; } = null!;
    public ApplicationUser? ChangedByAdmin { get; set; }
}
