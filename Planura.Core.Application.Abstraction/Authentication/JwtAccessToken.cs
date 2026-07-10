namespace Planura.Core.Application.Abstraction.Authentication
{
    public class JwtAccessToken
    {
        public string Token { get; init; } = null!;
        public DateTime ExpiresAtUtc { get; init; }
    }
}
