using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models.Client;

public class UpdateClientProfileDto
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = null!;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = null!;

    [Phone]
    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    public string? City { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public IFormFile? AvatarFile { get; set; }
}
