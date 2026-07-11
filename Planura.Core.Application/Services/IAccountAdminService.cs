using Planura.Core.Application.Models;

namespace Planura.Core.Application.Services;

public interface IAccountAdminService
{
    Task<AccountStatusDto> SuspendAsync(long userId);
    Task<AccountStatusDto> ReactivateAsync(long userId);
}
