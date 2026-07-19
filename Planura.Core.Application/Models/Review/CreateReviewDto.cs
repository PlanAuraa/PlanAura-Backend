using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models;

public class CreateReviewDto
{
    [Required]
    public long BookingRequestId { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }
}
