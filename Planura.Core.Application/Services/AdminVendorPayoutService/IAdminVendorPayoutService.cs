using Planura.Core.Application.Models;
using Planura.Core.Application.Models.AdminVendorPayout;

namespace Planura.Core.Application.Services.AdminVendorPayoutService
{
    public interface IAdminVendorPayoutService
    {
        Task<PagedResult<VendorFinancialSummaryDto>> ListVendorFinancialsAsync(VendorFinancialFilterDto filter);
        Task<VendorFinancialSummaryDto> GetVendorFinancialAsync(long vendorId);
        Task<VendorPayoutDto> RecordPayoutAsync(long vendorId, long adminId, RecordVendorPayoutDto dto);
        Task<List<VendorPayoutDto>> ListPayoutsAsync(long vendorId);
    }
}
