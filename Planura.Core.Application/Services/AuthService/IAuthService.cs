using Planura.Core.Application.Models;

namespace Planura.Core.Application.Services.Auth;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterClientAsync(RegisterClientDto dto);
    Task<AuthResponseDto> RegisterVendorAsync(RegisterVendorDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
    Task<CurrentUserDto> GetCurrentUserAsync();

    /// <summary>Updates the current user's own FullName/PhoneNumber/PreferredLanguage. General-purpose
    /// across all three roles (AdminDashboardPlan.md 2.16).</summary>
    Task<CurrentUserDto> UpdateMyProfileAsync(UpdateProfileDto dto);

    /// <summary>Changes the current user's own password, re-verifying the current one server-side.</summary>
    Task ChangePasswordAsync(ChangePasswordDto dto);
}
