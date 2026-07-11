using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services;

namespace Planura.Apis.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [HttpPost("register/client")]
    public async Task<ActionResult<AuthResponseDto>> RegisterClient([FromBody] RegisterClientDto dto)
    {
        return Ok(await _authService.RegisterClientAsync(dto));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
    {
        return Ok(await _authService.LoginAsync(dto));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserDto>> Me()
    {
        return Ok(await _authService.GetCurrentUserAsync());
    }
}
