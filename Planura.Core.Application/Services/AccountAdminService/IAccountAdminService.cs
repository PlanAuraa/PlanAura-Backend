using Planura.Core.Application.Models;
using Planura.Core.Application.Models.Admin;

namespace Planura.Core.Application.Services.AccountAdmin;

public interface IAccountAdminService
{
    Task<AccountStatusDto> SuspendAsync(long userId);
    Task<AccountStatusDto> ReactivateAsync(long userId);

    /// <summary>Lists every account in the Admin role (AdminDashboardPlan.md 2.13).</summary>
    Task<IEnumerable<AdminAccountDto>> ListAdminsAsync();

    /// <summary>Creates a new admin account, admin-triggered instead of seed-triggered.</summary>
    Task<AdminAccountDto> CreateAdminAsync(CreateAdminDto dto);
}
