using System.ComponentModel.DataAnnotations;
using Planura.Core.Domain.Enums;

namespace Planura.Core.Application.Abstraction.Vendors.Contracts
{
    public class BusinessInfoDto
    {
        [Required, StringLength(200)]
        public string BusinessName { get; set; } = null!;

        [StringLength(2000)]
        public string? BusinessDescription { get; set; }

        [Required]
        public VendorBusinessType BusinessType { get; set; }

        [Required]
        public Guid CategoryId { get; set; }
    }
}
