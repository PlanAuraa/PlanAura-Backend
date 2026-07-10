namespace Planura.Core.Application.Abstraction.Authentication
{
    public interface IJwtTokenGenerator
    {
        JwtAccessToken GenerateAccessToken(Guid userId, string email, string fullName, IEnumerable<string> roles);
    }
}
