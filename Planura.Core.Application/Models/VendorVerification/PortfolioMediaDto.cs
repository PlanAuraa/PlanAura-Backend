using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planura.Core.Application.Models.VendorVerification
{
    public class PortfolioMediaDto
    {
        public string FileUrl { get; set; } = null!;

        public string? Title { get; set; }

        public int DisplayOrder { get; set; }
    }
}
