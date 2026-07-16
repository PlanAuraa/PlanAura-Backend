namespace Planura.Core.Application.Models.Client;

/// <summary>
/// Combines the fields the client-facing "My Profile" page needs: identity
/// fields live on ApplicationUser (FullName/Email/PhoneNumber), the rest live
/// on the Client entity itself (City/DateOfBirth/AvatarUrl).
/// </summary>
public class ClientProfileDto
{
    public long Id { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string? City { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? AvatarUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
