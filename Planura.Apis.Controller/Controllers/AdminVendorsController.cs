using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models.AdminVendor;
using Planura.Core.Application.Services;
using Planura.Core.Application.Services.AdminVendor;

namespace Planura.Apis.Controllers;

/// <summary>
/// "All Vendors" admin management (AdminDashboardPlan.md 2.4) - every vendor regardless of
/// verification status. Detail and trust-promotion reuse the existing vendor-verification surface
/// (api/admin/vendor-verifications/{vendorId}[/trust]) rather than duplicating it here.
/// </summary>
[ApiController]
[Route("api/admin/vendors")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class AdminVendorsController : ControllerBase
{
    private readonly IAdminVendorService _adminVendorService;
    private readonly IVendorVerificationService _vendorVerificationService;

    public AdminVendorsController(
        IAdminVendorService adminVendorService,
        IVendorVerificationService vendorVerificationService)
    {
        _adminVendorService = adminVendorService;
        _vendorVerificationService = vendorVerificationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetVendors([FromQuery] AdminVendorFilterDto filter)
    {
        var result = await _adminVendorService.ListVendorsAsync(filter);
        return Ok(result);
    }

    [HttpGet("status-counts")]
    public async Task<IActionResult> GetStatusCounts()
    {
        var result = await _adminVendorService.GetStatusCountsAsync();
        return Ok(result);
    }

    /// <summary>Richer admin vendor detail - delegates to the vendor-verification detail view,
    /// which already includes documents, portfolio, UserId, and performance stats.</summary>
    [HttpGet("{vendorId:long}")]
    public async Task<IActionResult> GetVendorDetails(long vendorId)
    {
        var result = await _vendorVerificationService.GetVendorDetailsAsync(vendorId);
        return Ok(result);
    }

    /// <summary>Promotes an already-Verified vendor to the admin-curated Trusted tier.</summary>
    [HttpPost("{vendorId:long}/trust")]
    public async Task<IActionResult> PromoteToTrusted(long vendorId)
    {
        await _vendorVerificationService.PromoteToTrustedAsync(vendorId);
        return Ok(new { Message = "Vendor promoted to trusted successfully." });
    }
}
