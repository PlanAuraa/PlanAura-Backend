namespace Planura.Core.Application.Models;

public class UpdateServiceCategoryDto
{
    public string NameEn { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? IconUrl { get; set; }
    public bool IsActive { get; set; } = true;
}
