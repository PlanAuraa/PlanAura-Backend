namespace Planura.Core.Domain.Entities;

public class PortfolioLink
{
    public long Id { get; set; }
    public long VendorId { get; set; }
    public string Platform { get; set; } = null!;
    public string Url { get; set; } = null!;
    public string? Title { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Vendor Vendor { get; set; } = null!;
}
