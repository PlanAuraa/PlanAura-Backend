using Planura.Core.Domain.Entities.Identity;
using Planura.Core.Domain.Enums;

namespace Planura.Core.Domain.Entities.Vendors
{
    public class VerificationRequest
    {
        public Guid Id { get; set; }

        public Guid VendorProfileId { get; set; }
        public VendorProfile VendorProfile { get; set; } = null!;

        public VerificationStatus Status { get; set; } = VerificationStatus.Pending;

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }

        public Guid? ReviewedByUserId { get; set; }
        public ApplicationUser? ReviewedByUser { get; set; }

        public string? DecisionReason { get; set; }

        public byte[]? RowVersion { get; set; }

        public ICollection<VendorDocument> Documents { get; set; } = new List<VendorDocument>();
    }
}
