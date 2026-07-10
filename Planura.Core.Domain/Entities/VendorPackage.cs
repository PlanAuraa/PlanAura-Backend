namespace Planura.Core.Domain.Entities;

public class VendorPackage
{
    public long Id { get; set; }
    public long VendorId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public string Currency { get; set; } = "EGP";
    public int? MaxGuests { get; set; }
    public string? Includes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Vendor Vendor { get; set; } = null!;
    public ICollection<EventPlanItem> EventPlanItems { get; set; } = new List<EventPlanItem>();
    public ICollection<BookingRequest> BookingRequests { get; set; } = new List<BookingRequest>();
}
