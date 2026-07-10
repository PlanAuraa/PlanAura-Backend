using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Planura.Core.Application.Abstraction.Authentication;
using Planura.Core.Domain.Entities.Identity;
using Planura.Infrastructure.Persistence;

namespace Planura.Infrastructure.Authentication
{
    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly AppDbContext _dbContext;
        private readonly IConfiguration _configuration;

        public RefreshTokenService(AppDbContext dbContext, IConfiguration configuration)
        {
            _dbContext = dbContext;
            _configuration = configuration;
        }

        public async Task<RefreshTokenIssueResult> IssueAsync(Guid userId, string? createdByIp, CancellationToken ct = default)
        {
            var rawToken = GenerateRawToken();
            var expiresAt = DateTime.UtcNow.Add(GetLifetime());

            _dbContext.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = Hash(rawToken),
                ExpiresAt = expiresAt,
                CreatedByIp = createdByIp
            });

            await _dbContext.SaveChangesAsync(ct);

            return new RefreshTokenIssueResult { Token = rawToken, ExpiresAtUtc = expiresAt };
        }

        public async Task<RefreshTokenRotationResult> RotateAsync(string presentedRefreshToken, string? requestedByIp, CancellationToken ct = default)
        {
            var presentedHash = Hash(presentedRefreshToken);

            var existing = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(r => r.TokenHash == presentedHash, ct);

            if (existing is null)
            {
                return new RefreshTokenRotationResult { Succeeded = false, FailureReason = "Invalid refresh token." };
            }

            if (existing.RevokedAt is not null)
            {
                // The token was already rotated or revoked once before — presenting it again means
                // either a replay of a stale token or a stolen one. Revoke the whole active chain
                // for this user as a precaution rather than trusting this request.
                await RevokeAllActiveForUserAsync(existing.UserId, "Reuse detected", ct);
                return new RefreshTokenRotationResult { Succeeded = false, FailureReason = "Refresh token reuse detected; all sessions revoked." };
            }

            if (existing.IsExpired)
            {
                return new RefreshTokenRotationResult { Succeeded = false, FailureReason = "Refresh token expired." };
            }

            var newRawToken = GenerateRawToken();
            var newHash = Hash(newRawToken);
            var newExpiresAt = DateTime.UtcNow.Add(GetLifetime());

            existing.RevokedAt = DateTime.UtcNow;
            existing.ReplacedByTokenHash = newHash;
            existing.ReasonRevoked = "Rotated";

            _dbContext.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = existing.UserId,
                TokenHash = newHash,
                ExpiresAt = newExpiresAt,
                CreatedByIp = requestedByIp
            });

            await _dbContext.SaveChangesAsync(ct);

            return new RefreshTokenRotationResult
            {
                Succeeded = true,
                UserId = existing.UserId,
                NewRefreshToken = newRawToken,
                NewRefreshTokenExpiresAtUtc = newExpiresAt
            };
        }

        public async Task RevokeAsync(string presentedRefreshToken, string? reason = null, CancellationToken ct = default)
        {
            var hash = Hash(presentedRefreshToken);
            var existing = await _dbContext.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash, ct);

            if (existing is not null && existing.RevokedAt is null)
            {
                existing.RevokedAt = DateTime.UtcNow;
                existing.ReasonRevoked = reason ?? "Revoked";
                await _dbContext.SaveChangesAsync(ct);
            }
        }

        private async Task RevokeAllActiveForUserAsync(Guid userId, string reason, CancellationToken ct)
        {
            var activeTokens = await _dbContext.RefreshTokens
                .Where(r => r.UserId == userId && r.RevokedAt == null)
                .ToListAsync(ct);

            foreach (var token in activeTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
                token.ReasonRevoked = reason;
            }

            await _dbContext.SaveChangesAsync(ct);
        }

        private TimeSpan GetLifetime()
        {
            var days = double.TryParse(_configuration["Jwt:RefreshTokenLifetimeDays"], out var parsed) ? parsed : 7;
            return TimeSpan.FromDays(days);
        }

        private static string GenerateRawToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        private static string Hash(string rawToken) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }
}
