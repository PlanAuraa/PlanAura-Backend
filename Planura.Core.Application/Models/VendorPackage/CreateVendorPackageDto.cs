namespace Planura.Core.Application.Models;

public class CreateVendorPackageDto
{
    public long VendorId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public string Currency { get; set; } = "EGP";
    public int? MaxGuests { get; set; }
    public string? Includes { get; set; }
    public bool IsActive { get; set; } = true;
}
