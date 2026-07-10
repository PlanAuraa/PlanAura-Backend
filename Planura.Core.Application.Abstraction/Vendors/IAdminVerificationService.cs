using Planura.Core.Application.Abstraction.Vendors.Contracts;

namespace Planura.Core.Application.Abstraction.Vendors
{
    public interface IAdminVerificationService
    {
        Task<PagedResult<VendorApplicationSummaryDto>> GetPendingAsync(int page, int pageSize, CancellationToken ct = default);

        Task<VendorApplicationDetailsDto> GetDetailsAsync(Guid requestId, CancellationToken ct = default);

        Task<VendorStatusResponse> ApproveAsync(Guid requestId, Guid adminUserId, CancellationToken ct = default);

        Task<VendorStatusResponse> RejectAsync(Guid requestId, Guid adminUserId, RejectApplicationRequest request, CancellationToken ct = default);

        Task<VerificationHistoryDto> GetVendorHistoryAsync(Guid vendorProfileId, CancellationToken ct = default);
    }
}
