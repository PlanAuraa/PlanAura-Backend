namespace Planura.Core.Domain.Entities;

public class PortfolioMedia
{
    public long Id { get; set; }
    public long VendorId { get; set; }
    public string MediaType { get; set; } = null!;
    public string FileUrl { get; set; } = null!;
    public string? ThumbnailUrl { get; set; }
    public string? Title { get; set; }
    public int? FileSizeKb { get; set; }
    public int? DurationSec { get; set; }
    public int DisplayOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Vendor Vendor { get; set; } = null!;
}
