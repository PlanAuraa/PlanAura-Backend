using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models.Vendor
{
    public class AddPortfolioMediaDto
    {
        [Required]
        public IFormFile File { get; set; } = null!;

        public string? Title { get; set; }
    }
}
