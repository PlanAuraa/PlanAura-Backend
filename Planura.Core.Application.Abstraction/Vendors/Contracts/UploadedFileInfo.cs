namespace Planura.Core.Application.Abstraction.Vendors.Contracts
{
    // Keeps IFormFile out of the core layers — the Apis.Controller multipart form model maps
    // each IFormFile to this Stream+metadata shape before calling into the service layer.
    public class UploadedFileInfo
    {
        public Stream Content { get; set; } = null!;
        public string FileName { get; set; } = null!;
        public string ContentType { get; set; } = null!;
    }
}
