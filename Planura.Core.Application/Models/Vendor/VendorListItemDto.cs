namespace Planura.Core.Application.Models.Vendor
{
    public class VendorListItemDto
    {
        public long Id { get; set; }

        public string BusinessName { get; set; } = null!;

        public string? LogoUrl { get; set; }

        public string? Category { get; set; }

        public string? City { get; set; }

        public decimal? StartingPrice { get; set; }

        public decimal AvgRating { get; set; }

        public int ReviewCount { get; set; }

        public string VerificationStatus { get; set; } = null!;

        public string? ShortDescription { get; set; }
    }
}
