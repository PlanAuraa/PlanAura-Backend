namespace Planura.Core.Application.Abstraction.Storage
{
    public class VendorDocumentStreamResult
    {
        public Stream Stream { get; set; } = null!;
        public string ContentType { get; set; } = null!;
        public string FileName { get; set; } = null!;
    }
}
