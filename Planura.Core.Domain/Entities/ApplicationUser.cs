using Microsoft.AspNetCore.Identity;

namespace Planura.Core.Domain.Entities;

public class ApplicationUser : IdentityUser<long>
{
    public string FullName { get; set; } = null!;
    public string PreferredLanguage { get; set; } = "ar";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Client? Client { get; set; }
    public Vendor? Vendor { get; set; }
    public ICollection<VendorVerification> ReviewedVendorVerifications { get; set; } = new List<VendorVerification>();
    public ICollection<VendorVerificationHistory> VendorVerificationChanges { get; set; } = new List<VendorVerificationHistory>();
    public ICollection<BookingStatusHistory> BookingStatusChanges { get; set; } = new List<BookingStatusHistory>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
