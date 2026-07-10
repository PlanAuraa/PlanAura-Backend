using Planura.Core.Domain.Enums;

namespace Planura.Core.Application.Abstraction.Vendors.Contracts
{
    public class VendorDocumentMetadataDto
    {
        public Guid Id { get; set; }
        public DocumentType DocumentType { get; set; }
        public string ContentType { get; set; } = null!;
        public DateTime UploadedAt { get; set; }
    }
}
