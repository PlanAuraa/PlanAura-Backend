namespace Planura.Core.Application.Abstraction.Authentication.Contracts
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    }
}
