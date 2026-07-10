using Planura.Core.Application.Abstraction.Vendors.Contracts;

namespace Planura.Core.Application.Abstraction.Vendors
{
    public interface IVendorVerificationService
    {
        Task<VendorStatusResponse> GetMyStatusAsync(Guid userId, CancellationToken ct = default);

        Task<VerificationHistoryDto> GetMyHistoryAsync(Guid userId, CancellationToken ct = default);

        Task<VendorStatusResponse> ResubmitAsync(Guid userId, VendorResubmitRequest request, CancellationToken ct = default);
    }
}
