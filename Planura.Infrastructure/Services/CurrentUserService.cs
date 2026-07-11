using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Planura.Core.Application.Common;
using Planura.Core.Application.Services;

namespace Planura.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public long? UserId
        => long.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);

    public long? VendorId
        => long.TryParse(Principal?.FindFirstValue(CustomClaimTypes.VendorId), out var id) ? id : null;

    public IReadOnlyList<string> Roles
        => Principal?.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToList() ?? new List<string>();

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
