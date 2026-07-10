using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Abstraction.Vendors.Contracts
{
    public class LocationDto
    {
        [Required, StringLength(100)]
        public string City { get; set; } = null!;

        [Required, StringLength(100)]
        public string Area { get; set; } = null!;

        [Required, StringLength(300)]
        public string AddressLine { get; set; } = null!;

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
