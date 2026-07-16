using Planura.Core.Application.Models;
using Planura.Core.Application.Models.Vendor;

namespace Planura.Core.Application.Services;

public interface IVendorService
{
    Task<VendorDto> GetByIdAsync(long vendorId);
    Task<VendorDto> UpdateProfileAsync(long vendorId, UpdateVendorProfileDto dto);
    Task<PagedResult<VendorListItemDto>> BrowseVendorsAsync(VendorBrowseFilterDto filter);
    Task<VendorDashboardStatsDto> GetDashboardStatsAsync(long vendorId);

    Task<IEnumerable<PortfolioMediaItemDto>> GetPortfolioMediaAsync(long vendorId);
    Task<PortfolioMediaItemDto> AddPortfolioMediaAsync(long vendorId, AddPortfolioMediaDto dto);
    Task RemovePortfolioMediaAsync(long vendorId, long mediaId);
    Task ReorderPortfolioMediaAsync(long vendorId, ReorderPortfolioMediaDto dto);

    Task<IEnumerable<PortfolioLinkDto>> GetPortfolioLinksAsync(long vendorId);
    Task<PortfolioLinkDto> AddPortfolioLinkAsync(long vendorId, CreatePortfolioLinkDto dto);
    Task RemovePortfolioLinkAsync(long vendorId, long linkId);
}
