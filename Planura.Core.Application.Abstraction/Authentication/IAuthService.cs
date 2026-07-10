using Planura.Core.Application.Abstraction.Authentication.Contracts;

namespace Planura.Core.Application.Abstraction.Authentication
{
    public interface IAuthService
    {
        Task<AuthResponse> RegisterCustomerAsync(CustomerRegisterRequest request, string? ipAddress, CancellationToken ct = default);

        Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct = default);

        Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, string? ipAddress, CancellationToken ct = default);

        Task LogoutAsync(RefreshTokenRequest request, CancellationToken ct = default);

        Task<UserDto?> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);
    }
}
