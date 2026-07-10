namespace Planura.Core.Domain.Entities;

public class Review
{
    public long Id { get; set; }
    public long BookingRequestId { get; set; }
    public long ClientId { get; set; }
    public long VendorId { get; set; }
    public short Rating { get; set; }
    public string? Comment { get; set; }
    public bool IsHidden { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public BookingRequest BookingRequest { get; set; } = null!;
    public Client Client { get; set; } = null!;
    public Vendor Vendor { get; set; } = null!;
    public ReviewResponse? Response { get; set; }
}
