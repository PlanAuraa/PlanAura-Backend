namespace Planura.Core.Application.Models;

public class ReviewDto
{
    public long Id { get; set; }
    public short Rating { get; set; }
    public string? Comment { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? ClientAvatarUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? VendorResponse { get; set; }
    public DateTimeOffset? VendorRespondedAt { get; set; }
}
