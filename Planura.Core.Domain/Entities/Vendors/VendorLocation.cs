namespace Planura.Core.Domain.Entities.Vendors
{
    public class VendorLocation
    {
        public string City { get; set; } = null!;
        public string Area { get; set; } = null!;
        public string AddressLine { get; set; } = null!;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
