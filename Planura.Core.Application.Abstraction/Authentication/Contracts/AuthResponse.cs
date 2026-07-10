namespace Planura.Core.Application.Abstraction.Authentication.Contracts
{
    public class AuthResponse
    {
        public string AccessToken { get; set; } = null!;
        public DateTime AccessTokenExpiresAtUtc { get; set; }

        public string RefreshToken { get; set; } = null!;
        public DateTime RefreshTokenExpiresAtUtc { get; set; }

        public UserDto User { get; set; } = null!;
    }
}
