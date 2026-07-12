using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models.Vendor
{
    public class ReorderPortfolioMediaDto
    {
        [Required]
        public List<long> OrderedMediaIds { get; set; } = new();
    }
}
