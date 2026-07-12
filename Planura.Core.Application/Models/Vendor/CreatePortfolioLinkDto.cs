using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models.Vendor
{
    public class CreatePortfolioLinkDto
    {
        [Required]
        [MaxLength(20)]
        public string Platform { get; set; } = null!;

        [Required]
        [Url]
        [MaxLength(500)]
        public string Url { get; set; } = null!;

        [MaxLength(200)]
        public string? Title { get; set; }
    }
}
