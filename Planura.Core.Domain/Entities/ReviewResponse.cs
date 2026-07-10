namespace Planura.Core.Domain.Entities;

public class ReviewResponse
{
    public long Id { get; set; }
    public long ReviewId { get; set; }
    public long VendorId { get; set; }
    public string Response { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Review Review { get; set; } = null!;
    public Vendor Vendor { get; set; } = null!;
}
