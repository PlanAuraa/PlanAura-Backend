namespace Planura.Core.Application.Models;

public class JwtTokenResult
{
    public string AccessToken { get; set; } = null!;
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
