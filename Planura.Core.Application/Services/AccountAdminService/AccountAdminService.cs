using Microsoft.AspNetCore.Identity;
using Planura.Core.Application.Models;
using Planura.Core.Application.Models.Admin;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services.AccountAdmin;

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

    public async Task<IEnumerable<AdminAccountDto>> ListAdminsAsync()
    {
        var admins = await _userManager.GetUsersInRoleAsync(Roles.Admin);

        return admins
            .OrderBy(user => user.FullName)
            .Select(user => new AdminAccountDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                PhoneNumber = user.PhoneNumber,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            });
    }

    public async Task<AdminAccountDto> CreateAdminAsync(CreateAdminDto dto)
    {
        var existing = await _userManager.FindByEmailAsync(dto.Email);
        if (existing is not null)
        {
            throw new BadRequestExeption($"An account with email '{dto.Email}' already exists.");
        }

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FullName = dto.FullName,
            PhoneNumber = dto.PhoneNumber
        };

        var created = await _userManager.CreateAsync(user, dto.Password);
        if (!created.Succeeded)
        {
            throw new BadRequestExeption(DescribeErrors(created));
        }

        var roleAssigned = await _userManager.AddToRoleAsync(user, Roles.Admin);
        if (!roleAssigned.Succeeded)
        {
            throw new BadRequestExeption(DescribeErrors(roleAssigned));
        }

        return new AdminAccountDto
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }

    private async Task<AccountStatusDto> SetActiveAsync(long userId, bool isActive)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new NotFoundExeption(nameof(ApplicationUser), userId);
        }

        if (!isActive)
        {
            await GuardAgainstRemovingLastActiveAdminAsync(user);
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

    /// <summary>
    /// Blocks suspending the last remaining active admin - with a single flat Admin role and no
    /// runtime way to promote a new one (fixed by CreateAdminAsync above), losing the last admin
    /// would lock the platform's own operators out. Flagged as a real gap by both
    /// AdminDashboardPlan.md 2.13 and the implementation review's Security Review section.
    /// </summary>
    private async Task GuardAgainstRemovingLastActiveAdminAsync(ApplicationUser user)
    {
        var isAdmin = await _userManager.IsInRoleAsync(user, Roles.Admin);
        if (!isAdmin || !user.IsActive)
        {
            return;
        }

        var admins = await _userManager.GetUsersInRoleAsync(Roles.Admin);
        var otherActiveAdmins = admins.Count(admin => admin.Id != user.Id && admin.IsActive);

        if (otherActiveAdmins == 0)
        {
            throw new BadRequestExeption("Cannot suspend the last remaining active admin account.");
        }
    }

    private static string DescribeErrors(IdentityResult result)
        => string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
}
