namespace Planura.Core.Application.Models;

public class VendorPackageSearchDto
{
    public string? Title { get; set; }
    public long? CategoryId { get; set; }
    public bool ActiveOnly { get; set; } = false;
}
