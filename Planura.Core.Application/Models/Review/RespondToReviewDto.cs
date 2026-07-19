using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models;

public class RespondToReviewDto
{
    [Required]
    [MaxLength(1000)]
    public string Response { get; set; } = string.Empty;
}
