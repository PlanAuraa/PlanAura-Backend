using Planura.Core.Application.Models;

namespace Planura.Core.Application.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterClientAsync(RegisterClientDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
    Task<CurrentUserDto> GetCurrentUserAsync();
}
