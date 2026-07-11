using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Infrastructure.Authorization;

public class ApprovedVendorHandler : AuthorizationHandler<ApprovedVendorRequirement>
{
    private readonly IUnitOfWork _unitOfWork;

    public ApprovedVendorHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ApprovedVendorRequirement requirement)
    {
        // Must be a vendor first.
        if (!context.User.IsInRole(Roles.Vendor))
        {
            return;
        }

        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdValue, out var userId))
        {
            return;
        }

        var vendor = await _unitOfWork.Repository<Vendor, long>()
            .GetWithSpecAsync(new VendorByUserIdSpecification(userId));

        if (vendor is not null && VerificationStatus.IsApproved(vendor.VerificationStatus))
        {
            context.Succeed(requirement);
        }
    }
}
