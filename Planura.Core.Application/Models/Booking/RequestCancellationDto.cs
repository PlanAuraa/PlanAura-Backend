using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models;

public class RequestCancellationDto
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = null!;
}
