using Planura.Core.Application.Models;
using Planura.Core.Domain.Entities;

namespace Planura.Core.Application.Services;

public interface ITokenService
{
    JwtTokenResult CreateToken(ApplicationUser user, IReadOnlyCollection<string> roles, long? vendorId = null);
}
