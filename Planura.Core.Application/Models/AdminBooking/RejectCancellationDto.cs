using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models.AdminBooking
{
    public class RejectCancellationDto
    {
        [Required]
        [MaxLength(500)]
        public string Note { get; set; } = null!;
    }
}
