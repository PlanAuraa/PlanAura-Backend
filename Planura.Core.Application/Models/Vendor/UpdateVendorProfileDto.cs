using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models.Vendor
{
    public class UpdateVendorProfileDto
    {
        [Required]
        public string BusinessName { get; set; } = null!;

        public string? BusinessDescription { get; set; }

        public long? CategoryId { get; set; }

        public string? City { get; set; }

        public string? Address { get; set; }

        public decimal? Latitude { get; set; }

        public decimal? Longitude { get; set; }

        public IFormFile? LogoFile { get; set; }

        public IFormFile? CoverImageFile { get; set; }
    }
}
