using Microsoft.AspNetCore.Identity;
using Planura.Core.Application.Models;
using Planura.Core.Domain.Entities;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services;

/// <summary>
/// Admin-only account access control. Suspension flips <see cref="ApplicationUser.IsActive"/>,
/// which the JWT bearer <c>OnTokenValidated</c> handler enforces on every request, so a suspended
/// user is blocked immediately even with a still-valid token. Verification history/status are untouched.
/// </summary>
public class AccountAdminService : IAccountAdminService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public AccountAdminService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public Task<AccountStatusDto> SuspendAsync(long userId) => SetActiveAsync(userId, isActive: false);

    public Task<AccountStatusDto> ReactivateAsync(long userId) => SetActiveAsync(userId, isActive: true);

    private async Task<AccountStatusDto> SetActiveAsync(long userId, bool isActive)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new NotFoundExeption(nameof(ApplicationUser), userId);
        }

        if (user.IsActive != isActive)
        {
            user.IsActive = isActive;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                throw new BadRequestExeption(
                    string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}")));
            }
        }

        return new AccountStatusDto { UserId = user.Id, IsActive = user.IsActive };
    }
}
