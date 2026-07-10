using Planura.Core.Domain.Enums;

namespace Planura.Core.Application.Abstraction.Vendors.Contracts
{
    public class VendorDocumentUpload
    {
        public DocumentType DocumentType { get; set; }
        public UploadedFileInfo File { get; set; } = null!;
    }
}
