using Planura.Core.Domain.Enums;

namespace Planura.Core.Domain.Entities.Vendors
{
    public class VendorDocument
    {
        public Guid Id { get; set; }

        public Guid VerificationRequestId { get; set; }
        public VerificationRequest VerificationRequest { get; set; } = null!;

        public DocumentType DocumentType { get; set; }
        public string StoredPath { get; set; } = null!;
        public string ContentType { get; set; } = null!;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
