using Microsoft.AspNetCore.Http;

namespace Planura.Core.Application.Models;

public class CreateServiceCategoryDto
{
    public string NameEn { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public IFormFile? IconFile { get; set; }
    public bool IsActive { get; set; } = true;
}
