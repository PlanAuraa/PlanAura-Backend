namespace Planura.Core.Application.Models;

public class ServiceCategoryDto
{
    public long Id { get; set; }
    public string NameEn { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? IconUrl { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
