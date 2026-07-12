using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Planura.Core.Domain.Enums;

namespace Planura.Core.Domain.Entities
{
    public class VendorVerificationDocument
    {
        public long Id { get; set; }

        public long VendorVerificationId { get; set; }

        public VerificationDocumentType DocumentType { get; set; }

        public string FileUrl { get; set; } = null!;

        public string? OriginalFileName { get; set; }

        public string? ContentType { get; set; }

        public long? FileSizeBytes { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public VendorVerification VendorVerification { get; set; } = null!;
    }
}
