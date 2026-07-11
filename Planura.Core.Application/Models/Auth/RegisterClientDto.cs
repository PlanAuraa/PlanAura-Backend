using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models;

public class RegisterClientDto
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = null!;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = null!;

    [Required]
    [Phone]
    [StringLength(20)]
    public string PhoneNumber { get; set; } = null!;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = null!;

    [Required]
    [Compare(nameof(Password), ErrorMessage = "Password and ConfirmPassword do not match.")]
    public string ConfirmPassword { get; set; } = null!;
}
