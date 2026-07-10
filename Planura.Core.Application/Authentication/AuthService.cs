using Microsoft.AspNetCore.Identity;
using Planura.Core.Application.Abstraction.Authentication;
using Planura.Core.Application.Abstraction.Authentication.Contracts;
using Planura.Core.Domain.Entities.Identity;
using Planura.Shared.Constants;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Authentication
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IJwtTokenGenerator _jwtTokenGenerator;
        private readonly IRefreshTokenService _refreshTokenService;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IJwtTokenGenerator jwtTokenGenerator,
            IRefreshTokenService refreshTokenService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtTokenGenerator = jwtTokenGenerator;
            _refreshTokenService = refreshTokenService;
        }

        public async Task<AuthResponse> RegisterCustomerAsync(CustomerRegisterRequest request, string? ipAddress, CancellationToken ct = default)
        {
            var existing = await _userManager.FindByEmailAsync(request.Email);
            if (existing is not null)
            {
                throw new BadRequestExeption("An account with this email already exists.");
            }

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                EmailConfirmed = true // email confirmation is deferred to a later phase
            };

            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(" ", createResult.Errors.Select(e => e.Description));
                throw new BadRequestExeption(errors);
            }

            await _userManager.AddToRoleAsync(user, Roles.Customer);

            return await BuildAuthResponseAsync(user, ipAddress, ct);
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct = default)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user is null)
            {
                throw new UnAuthorizedExeption("Invalid email or password.");
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                throw new UnAuthorizedExeption(result.IsLockedOut
                    ? "This account is temporarily locked due to multiple failed login attempts."
                    : "Invalid email or password.");
            }

            return await BuildAuthResponseAsync(user, ipAddress, ct);
        }

        public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, string? ipAddress, CancellationToken ct = default)
        {
            var rotation = await _refreshTokenService.RotateAsync(request.RefreshToken, ipAddress, ct);
            if (!rotation.Succeeded)
            {
                throw new UnAuthorizedExeption(rotation.FailureReason ?? "Invalid refresh token.");
            }

            var user = await _userManager.FindByIdAsync(rotation.UserId.ToString())
                ?? throw new UnAuthorizedExeption("User no longer exists.");

            var roles = await _userManager.GetRolesAsync(user);
            var accessToken = _jwtTokenGenerator.GenerateAccessToken(user.Id, user.Email!, user.FullName, roles);

            return new AuthResponse
            {
                AccessToken = accessToken.Token,
                AccessTokenExpiresAtUtc = accessToken.ExpiresAtUtc,
                RefreshToken = rotation.NewRefreshToken!,
                RefreshTokenExpiresAtUtc = rotation.NewRefreshTokenExpiresAtUtc,
                User = MapToDto(user, roles)
            };
        }

        public async Task LogoutAsync(RefreshTokenRequest request, CancellationToken ct = default)
        {
            await _refreshTokenService.RevokeAsync(request.RefreshToken, "Logout", ct);
        }

        public async Task<UserDto?> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return null;
            }

            var roles = await _userManager.GetRolesAsync(user);
            return MapToDto(user, roles);
        }

        private async Task<AuthResponse> BuildAuthResponseAsync(ApplicationUser user, string? ipAddress, CancellationToken ct)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var accessToken = _jwtTokenGenerator.GenerateAccessToken(user.Id, user.Email!, user.FullName, roles);
            var refreshToken = await _refreshTokenService.IssueAsync(user.Id, ipAddress, ct);

            return new AuthResponse
            {
                AccessToken = accessToken.Token,
                AccessTokenExpiresAtUtc = accessToken.ExpiresAtUtc,
                RefreshToken = refreshToken.Token,
                RefreshTokenExpiresAtUtc = refreshToken.ExpiresAtUtc,
                User = MapToDto(user, roles)
            };
        }

        private static UserDto MapToDto(ApplicationUser user, IList<string> roles) => new()
        {
            Id = user.Id,
            Email = user.Email!,
            FullName = user.FullName,
            Roles = roles.ToArray()
        };
    }
}
