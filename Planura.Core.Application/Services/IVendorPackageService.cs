using Planura.Core.Application.Models;

namespace Planura.Core.Application.Services;

public interface IVendorPackageService
{
    Task<IEnumerable<VendorPackageDto>> GetAllAsync(bool activeOnly = false);
    Task<IEnumerable<VendorPackageDto>> GetByVendorAsync(long vendorId, bool activeOnly = false);
    Task<VendorPackageDto> GetByIdAsync(long id);
    Task<VendorPackageDto> CreateAsync(CreateVendorPackageDto dto);
    Task<VendorPackageDto> UpdateAsync(long id, UpdateVendorPackageDto dto);
    Task DeleteAsync(long id);
    Task<IEnumerable<VendorPackageDto>> SearchAsync(VendorPackageSearchDto query);
}
