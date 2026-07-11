namespace Planura.Core.Application.Models;

public class CurrentUserDto
{
    public long UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = new List<string>();
}
