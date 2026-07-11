namespace Planura.Core.Application.Services;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    long? UserId { get; }
    string? Email { get; }
    long? VendorId { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsInRole(string role);
}
