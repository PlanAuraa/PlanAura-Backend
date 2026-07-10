namespace Planura.Core.Application.Models;

public class VendorPackageDto
{
    public long Id { get; set; }
    public long VendorId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public string Currency { get; set; } = null!;
    public int? MaxGuests { get; set; }
    public string? Includes { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
