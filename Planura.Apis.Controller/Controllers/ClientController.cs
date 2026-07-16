using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models.Client;
using Planura.Core.Application.Services;
using Planura.Shared.Errors.Models;

namespace Planura.Apis.Controllers;

[ApiController]
[Route("api/clients")]
public class ClientController : ControllerBase
{
    private readonly IClientService _clientService;
    private readonly ICurrentUserService _currentUserService;

    public ClientController(IClientService clientService, ICurrentUserService currentUserService)
    {
        _clientService = clientService;
        _currentUserService = currentUserService;
    }

    [Authorize(Policy = AuthorizationPolicies.ClientOnly)]
    [HttpGet("me")]
    public async Task<ActionResult<ClientProfileDto>> GetMyProfile()
    {
        var userId = _currentUserService.UserId
            ?? throw new UnAuthorizedExeption("No authenticated user.");

        return Ok(await _clientService.GetMyProfileAsync(userId));
    }

    [Authorize(Policy = AuthorizationPolicies.ClientOnly)]
    [HttpPut("me")]
    public async Task<ActionResult<ClientProfileDto>> UpdateMyProfile([FromForm] UpdateClientProfileDto dto)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnAuthorizedExeption("No authenticated user.");

        return Ok(await _clientService.UpdateMyProfileAsync(userId, dto));
    }
}
