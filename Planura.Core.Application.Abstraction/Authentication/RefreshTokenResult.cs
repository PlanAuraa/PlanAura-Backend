namespace Planura.Core.Application.Abstraction.Authentication
{
    public class RefreshTokenIssueResult
    {
        public string Token { get; init; } = null!;
        public DateTime ExpiresAtUtc { get; init; }
    }

    public class RefreshTokenRotationResult
    {
        public bool Succeeded { get; init; }
        public Guid UserId { get; init; }
        public string? NewRefreshToken { get; init; }
        public DateTime NewRefreshTokenExpiresAtUtc { get; init; }
        public string? FailureReason { get; init; }
    }
}
