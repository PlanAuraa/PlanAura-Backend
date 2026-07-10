using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Planura.Core.Domain.Enums;
using Planura.Infrastructure.Persistence;
using Planura.Shared.Constants;

namespace Planura.Infrastructure.Authorization
{
    public class ApprovedVendorHandler : AuthorizationHandler<ApprovedVendorRequirement>
    {
        private readonly AppDbContext _dbContext;

        public ApprovedVendorHandler(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            ApprovedVendorRequirement requirement)
        {
            if (!context.User.IsInRole(Roles.Vendor))
            {
                return;
            }

            var userIdValue = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (!Guid.TryParse(userIdValue, out var userId))
            {
                return;
            }

            // Queried live on every request, not cached — approval must take effect
            // immediately without requiring the vendor to log in again.
            var isApproved = await _dbContext.VendorProfiles
                .AsNoTracking()
                .AnyAsync(v => v.UserId == userId && v.Status == VerificationStatus.Approved);

            if (isApproved)
            {
                context.Succeed(requirement);
            }
        }
    }
}
