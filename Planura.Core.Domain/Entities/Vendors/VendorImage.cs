namespace Planura.Core.Domain.Entities.Vendors
{
    public class VendorImage
    {
        public Guid Id { get; set; }

        public Guid VendorProfileId { get; set; }
        public VendorProfile VendorProfile { get; set; } = null!;

        public string StoredPath { get; set; } = null!;
        public string ContentType { get; set; } = null!;
        public bool IsPrimary { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
