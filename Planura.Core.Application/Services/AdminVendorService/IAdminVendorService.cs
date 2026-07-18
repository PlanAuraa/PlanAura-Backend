using Planura.Core.Application.Models;
using Planura.Core.Application.Models.AdminVendor;

namespace Planura.Core.Application.Services.AdminVendor
{
    public interface IAdminVendorService
    {
        Task<PagedResult<AdminVendorListItemDto>> ListVendorsAsync(AdminVendorFilterDto filter);

        Task<AdminVendorStatusCountsDto> GetStatusCountsAsync();
    }
}
