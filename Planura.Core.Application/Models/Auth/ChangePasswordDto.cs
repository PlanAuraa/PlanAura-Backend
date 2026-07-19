using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models;

public class ChangePasswordDto
{
    [Required]
    public string CurrentPassword { get; set; } = null!;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string NewPassword { get; set; } = null!;

    [Required]
    [Compare(nameof(NewPassword), ErrorMessage = "NewPassword and ConfirmNewPassword do not match.")]
    public string ConfirmNewPassword { get; set; } = null!;
}
