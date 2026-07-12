using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using Planura.Core.Domain.Enums;

namespace Planura.Core.Application.Models;

public class RegisterVendorDto
{
    // User
    [Required]
    public string FullName { get; set; } = null!;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    public string PhoneNumber { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;

    [Required]
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = null!;

    // Vendor
    [Required]
    public string BusinessName { get; set; } = null!;
    public string? BusinessDescription { get; set; }
    public long? CategoryId { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }

    // NEW
    [Required]
    public VendorType VendorType { get; set; }

    // Documents
    [Required]
    public IFormFile NationalIdFront { get; set; } = null!;

    [Required]
    public IFormFile NationalIdBack { get; set; } = null!;

    [Required]
    public IFormFile SelfieWithId { get; set; } = null!;
    public IFormFile? CommercialRegistration { get; set; }
    [Required]
    public List<IFormFile> PortfolioImages { get; set; } = new();
    public IFormFile? TaxCard { get; set; }
}
