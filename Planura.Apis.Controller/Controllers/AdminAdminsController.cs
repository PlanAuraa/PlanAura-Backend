using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models.Admin;
using Planura.Core.Application.Services.AccountAdmin;

namespace Planura.Apis.Controllers;

/// <summary>
/// "Admin Accounts" management (AdminDashboardPlan.md 2.13) - list/create additional admins.
/// Suspend/reactivate for an admin account reuse the existing, role-agnostic
/// AdminAccountsController (api/admin/users/{userId}/suspend|reactivate), which now also guards
/// against removing the last active admin (see AccountAdminService).
/// </summary>
[ApiController]
[Route("api/admin/admins")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class AdminAdminsController : ControllerBase
{
    private readonly IAccountAdminService _accountAdminService;

    public AdminAdminsController(IAccountAdminService accountAdminService)
    {
        _accountAdminService = accountAdminService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdminAccountDto>>> GetAdmins()
    {
        return Ok(await _accountAdminService.ListAdminsAsync());
    }

    [HttpPost]
    public async Task<ActionResult<AdminAccountDto>> CreateAdmin([FromBody] CreateAdminDto dto)
    {
        var created = await _accountAdminService.CreateAdminAsync(dto);
        return CreatedAtAction(nameof(GetAdmins), created);
    }
}
