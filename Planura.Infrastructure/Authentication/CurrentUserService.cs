using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using Planura.Core.Application.Abstraction.Authentication;

namespace Planura.Infrastructure.Authentication
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid? UserId
        {
            get
            {
                var value = _httpContextAccessor.HttpContext?.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                return Guid.TryParse(value, out var id) ? id : null;
            }
        }

        public string? Email => _httpContextAccessor.HttpContext?.User?.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

        public bool IsInRole(string role) => _httpContextAccessor.HttpContext?.User?.IsInRole(role) ?? false;
    }
}
