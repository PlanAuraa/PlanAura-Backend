using Planura.Core.Application.Models;

namespace Planura.Core.Application.Services.CategoryService;

public interface IServiceCategoryService
{
    Task<IEnumerable<ServiceCategoryDto>> GetAllAsync(bool activeOnly = false);
    Task<ServiceCategoryDto> GetByIdAsync(long id);
    Task<ServiceCategoryDto> GetBySlugAsync(string slug);
    Task<ServiceCategoryDto> CreateAsync(CreateServiceCategoryDto dto);
    Task<ServiceCategoryDto> UpdateAsync(long id, UpdateServiceCategoryDto dto);
    Task DeleteAsync(long id);
}
