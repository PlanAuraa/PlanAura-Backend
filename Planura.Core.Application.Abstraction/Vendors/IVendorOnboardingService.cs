using Planura.Core.Application.Abstraction.Authentication.Contracts;
using Planura.Core.Application.Abstraction.Vendors.Contracts;

namespace Planura.Core.Application.Abstraction.Vendors
{
    public interface IVendorOnboardingService
    {
        Task<AuthResponse> RegisterAsync(VendorRegisterRequest request, string? ipAddress, CancellationToken ct = default);
    }
}
