namespace Planura.Core.Application.Abstraction.Authentication
{
    public interface IRefreshTokenService
    {
        Task<RefreshTokenIssueResult> IssueAsync(Guid userId, string? createdByIp, CancellationToken ct = default);

        Task<RefreshTokenRotationResult> RotateAsync(string presentedRefreshToken, string? requestedByIp, CancellationToken ct = default);

        Task RevokeAsync(string presentedRefreshToken, string? reason = null, CancellationToken ct = default);
    }
}
