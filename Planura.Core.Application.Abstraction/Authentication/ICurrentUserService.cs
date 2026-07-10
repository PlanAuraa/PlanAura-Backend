namespace Planura.Core.Application.Abstraction.Authentication
{
    public interface ICurrentUserService
    {
        Guid? UserId { get; }
        string? Email { get; }
        bool IsInRole(string role);
    }
}
