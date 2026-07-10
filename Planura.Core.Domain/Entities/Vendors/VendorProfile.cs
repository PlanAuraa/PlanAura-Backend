using Planura.Core.Domain.Entities.Identity;
using Planura.Core.Domain.Enums;

namespace Planura.Core.Domain.Entities.Vendors
{
    public class VendorProfile
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;

        public string BusinessName { get; set; } = null!;
        public string? BusinessDescription { get; set; }
        public VendorBusinessType BusinessType { get; set; }

        public Guid CategoryId { get; set; }
        public VendorCategory Category { get; set; } = null!;

        public VendorLocation Location { get; set; } = null!;

        public VerificationStatus Status { get; set; } = VerificationStatus.Pending;

        public Guid? CurrentVerificationRequestId { get; set; }
        public VerificationRequest? CurrentVerificationRequest { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<VendorImage> Images { get; set; } = new List<VendorImage>();
        public ICollection<VerificationRequest> VerificationRequests { get; set; } = new List<VerificationRequest>();
    }
}
