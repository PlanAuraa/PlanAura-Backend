namespace Planura.Core.Application.Models;

public class ServiceCategoryDto
{
    public long Id { get; set; }
    public string NameEn { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? IconUrl { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Number of vendors currently attached to this category - lets an admin see usage
    /// before deleting/deactivating (deleting a category with vendors attached orphans
    /// Vendor.CategoryId; prefer IsActive = false once VendorCount &gt; 0).</summary>
    public int VendorCount { get; set; }
}
