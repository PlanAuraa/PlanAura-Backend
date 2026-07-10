using Planura.Core.Domain.Enums;

namespace Planura.Core.Application.Abstraction.Vendors.Contracts
{
    public class VendorApplicationDetailsDto
    {
        public Guid RequestId { get; set; }
        public Guid VendorProfileId { get; set; }
        public string BusinessName { get; set; } = null!;
        public string? BusinessDescription { get; set; }
        public VendorBusinessType BusinessType { get; set; }
        public string CategoryName { get; set; } = null!;
        public LocationDto Location { get; set; } = null!;
        public DateTime SubmittedAt { get; set; }
        public List<VendorDocumentMetadataDto> Documents { get; set; } = new();
        public List<string> ImageUrls { get; set; } = new();
    }
}
