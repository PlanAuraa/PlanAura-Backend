using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Abstraction.Authentication;
using Planura.Core.Application.Abstraction.Authentication.Contracts;

namespace Planura.Apis.Controller.Controllers.Auth
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ICurrentUserService _currentUserService;

        public AuthController(IAuthService authService, ICurrentUserService currentUserService)
        {
            _authService = authService;
            _currentUserService = currentUserService;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Register(CustomerRegisterRequest request, CancellationToken ct)
        {
            var result = await _authService.RegisterCustomerAsync(request, GetClientIp(), ct);
            return Ok(result);
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
        {
            var result = await _authService.LoginAsync(request, GetClientIp(), ct);
            return Ok(result);
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Refresh(RefreshTokenRequest request, CancellationToken ct)
        {
            var result = await _authService.RefreshAsync(request, GetClientIp(), ct);
            return Ok(result);
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout(RefreshTokenRequest request, CancellationToken ct)
        {
            await _authService.LogoutAsync(request, ct);
            return NoContent();
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
        {
            if (_currentUserService.UserId is not { } userId)
            {
                return Unauthorized();
            }

            var user = await _authService.GetCurrentUserAsync(userId, ct);
            return user is null ? NotFound() : Ok(user);
        }

        private string? GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
