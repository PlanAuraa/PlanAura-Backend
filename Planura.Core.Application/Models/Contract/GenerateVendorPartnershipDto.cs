using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models;

public class GenerateVendorPartnershipDto
{
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

    [StringLength(100)]
    public string? VendorCategory { get; set; }

    [StringLength(100)]
    public string? VendorCity { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal? CommissionRatePercent { get; set; }

    public DateOnly? EffectiveDate { get; set; }

    [StringLength(150)]
    public string? PlanuraRepresentativeName { get; set; }

    [StringLength(2000)]
    public string? AdditionalTerms { get; set; }
}
