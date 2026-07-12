using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models.VendorVerification
{
    public class ResubmitVerificationDto
    {
        [Required]
        public IFormFile NationalIdFront { get; set; } = null!;

        [Required]
        public IFormFile NationalIdBack { get; set; } = null!;

        [Required]
        public IFormFile SelfieWithId { get; set; } = null!;

        public IFormFile? CommercialRegistration { get; set; }

        public IFormFile? TaxCard { get; set; }
    }
}
