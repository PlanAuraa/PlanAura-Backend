namespace Planura.Core.Domain.Entities;

public class ServiceCategory
{
    public long Id { get; set; }
    public string NameEn { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? IconUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Vendor> Vendors { get; set; } = new List<Vendor>();
}
