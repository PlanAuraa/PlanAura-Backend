using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services;

namespace Planura.Apis.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class AdminAccountsController : ControllerBase
{
    private readonly IAccountAdminService _accountAdminService;

    public AdminAccountsController(IAccountAdminService accountAdminService)
    {
        _accountAdminService = accountAdminService;
    }

    [HttpPost("{userId:long}/suspend")]
    public async Task<ActionResult<AccountStatusDto>> Suspend(long userId)
    {
        return Ok(await _accountAdminService.SuspendAsync(userId));
    }

    [HttpPost("{userId:long}/reactivate")]
    public async Task<ActionResult<AccountStatusDto>> Reactivate(long userId)
    {
        return Ok(await _accountAdminService.ReactivateAsync(userId));
    }
}
