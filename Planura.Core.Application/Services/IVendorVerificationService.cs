using Planura.Core.Application.Models.VendorVerification;

namespace Planura.Core.Application.Services;

public interface IVendorVerificationService
{
    Task ApproveVendorAsync(long vendorId);

    Task RejectVendorAsync(long vendorId, string rejectionReason);

    Task<IEnumerable<PendingVendorDto>> GetPendingVendorsAsync();
    Task<VendorDetailsDto> GetVendorDetailsAsync(long vendorId);
    Task<IEnumerable<VendorVerificationHistoryDto>> GetVerificationHistoryAsync(long vendorId);
    Task<VendorVerificationStatusDto> ResubmitVerificationAsync(long vendorId, ResubmitVerificationDto dto);

    /// <summary>Promotes an already-Verified vendor to the admin-curated Trusted tier.</summary>
    Task PromoteToTrustedAsync(long vendorId);
}