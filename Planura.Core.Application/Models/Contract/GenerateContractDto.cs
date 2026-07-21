using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models;

public class GenerateContractDto
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string ClientName { get; set; } = null!;

    [EmailAddress]
    [StringLength(256)]
    public string? ClientEmail { get; set; }

    [Phone]
    [StringLength(30)]
    public string? ClientPhone { get; set; }

    [StringLength(300)]
    public string? ClientAddress { get; set; }

    [StringLength(150)]
    public string? ClientRepresentativeName { get; set; }

    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string VendorName { get; set; } = null!;

    [EmailAddress]
    [StringLength(256)]
    public string? VendorEmail { get; set; }

    [Phone]
    [StringLength(30)]
    public string? VendorPhone { get; set; }

    [StringLength(300)]
    public string? VendorAddress { get; set; }

    [StringLength(150)]
    public string? VendorRepresentativeName { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string EventType { get; set; } = null!;

    [Required]
    public DateOnly EventDate { get; set; }

    [StringLength(300)]
    public string? EventLocation { get; set; }

    [Range(1, 1_000_000)]
    public int? GuestCount { get; set; }

    [Required]
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Price { get; set; }

    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "EGP";

    [StringLength(2000)]
    public string? AdditionalTerms { get; set; }
}
