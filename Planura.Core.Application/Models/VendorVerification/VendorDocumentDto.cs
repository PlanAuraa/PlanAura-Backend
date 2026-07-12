using Planura.Core.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planura.Core.Application.Models.VendorVerification
{
    public class VendorDocumentDto
    {
        public VerificationDocumentType DocumentType { get; set; }

        public string FileUrl { get; set; } = null!;

        public string? OriginalFileName { get; set; }

        public string? ContentType { get; set; }

        public long? FileSizeBytes { get; set; }
    }
}
