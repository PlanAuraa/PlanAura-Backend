using Microsoft.AspNetCore.Identity;
using Planura.Core.Application.Models;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ICurrentUserService _currentUserService;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ICurrentUserService currentUserService)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _currentUserService = currentUserService;
    }

    public async Task<AuthResponseDto> RegisterClientAsync(RegisterClientDto dto)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
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

            var roleAssigned = await _userManager.AddToRoleAsync(user, Roles.Client);
            if (!roleAssigned.Succeeded)
            {
                throw new BadRequestExeption(DescribeErrors(roleAssigned));
            }

            var client = new Client { UserId = user.Id };
            await _unitOfWork.Repository<Client, long>().AddAsync(client);

            await _unitOfWork.CommitTransactionAsync();

            var roles = await _userManager.GetRolesAsync(user);
            return BuildAuthResponse(user, roles);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, dto.Password))
        {
            throw new UnAuthorizedExeption("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            throw new UnAuthorizedExeption("This account has been suspended.");
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);

        long? vendorId = null;
        if (roles.Contains(Roles.Vendor))
        {
            var vendor = await _unitOfWork.Repository<Vendor, long>()
                .GetWithSpecAsync(new VendorByUserIdSpecification(user.Id));
            vendorId = vendor?.Id;
        }

        return BuildAuthResponse(user, roles, vendorId);
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync()
    {
        var userId = _currentUserService.UserId
            ?? throw new UnAuthorizedExeption("No authenticated user.");

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new NotFoundExeption(nameof(ApplicationUser), userId);
        }

        var roles = await _userManager.GetRolesAsync(user);

        return new CurrentUserDto
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            PhoneNumber = user.PhoneNumber,
            Roles = roles.ToList()
        };
    }

    private AuthResponseDto BuildAuthResponse(ApplicationUser user, IList<string> roles, long? vendorId = null)
    {
        var token = _tokenService.CreateToken(user, roles.ToList(), vendorId);

        return new AuthResponseDto
        {
            AccessToken = token.AccessToken,
            ExpiresAtUtc = token.ExpiresAtUtc,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            Roles = roles.ToList()
        };
    }

    private static string DescribeErrors(IdentityResult result)
        => string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
}
