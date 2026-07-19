using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models.AdminClient;
using Planura.Core.Application.Services.AdminClient;

namespace Planura.Apis.Controllers;

/// <summary>"Client Management" admin surface (AdminDashboardPlan.md 2.5).</summary>
[ApiController]
[Route("api/admin/clients")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class AdminClientsController : ControllerBase
{
    private readonly IAdminClientService _adminClientService;

    public AdminClientsController(IAdminClientService adminClientService)
    {
        _adminClientService = adminClientService;
    }

    [HttpGet]
    public async Task<IActionResult> GetClients([FromQuery] AdminClientFilterDto filter)
    {
        var result = await _adminClientService.ListClientsAsync(filter);
        return Ok(result);
    }

    [HttpGet("{clientId:long}")]
    public async Task<IActionResult> GetClientDetails(long clientId)
    {
        var result = await _adminClientService.GetClientDetailsAsync(clientId);
        return Ok(result);
    }
}
