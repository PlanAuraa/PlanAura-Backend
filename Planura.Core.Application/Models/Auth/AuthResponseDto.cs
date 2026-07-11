namespace Planura.Core.Application.Models;

public class AuthResponseDto
{
    public string AccessToken { get; set; } = null!;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public long UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public IReadOnlyList<string> Roles { get; set; } = new List<string>();
}
