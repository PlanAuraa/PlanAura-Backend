using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models;

public class UpdateReviewDto
{
    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }
}
