using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models.AdminVendorPayout;
using Planura.Core.Application.Services.AdminVendorPayoutService;
using System.Security.Claims;

namespace Planura.Apis.Controllers;

/// <summary>"Vendor Payables" admin surface: what's owed to each vendor and manual payout recording.</summary>
[ApiController]
[Route("api/admin/vendor-payouts")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class AdminVendorPayoutsController : ControllerBase
{
    private readonly IAdminVendorPayoutService _adminVendorPayoutService;

    public AdminVendorPayoutsController(IAdminVendorPayoutService adminVendorPayoutService)
    {
        _adminVendorPayoutService = adminVendorPayoutService;
    }

    [HttpGet]
    public async Task<IActionResult> GetVendorFinancials([FromQuery] VendorFinancialFilterDto filter)
    {
        var result = await _adminVendorPayoutService.ListVendorFinancialsAsync(filter);
        return Ok(result);
    }

    [HttpGet("{vendorId:long}")]
    public async Task<IActionResult> GetVendorFinancial(long vendorId)
    {
        var result = await _adminVendorPayoutService.GetVendorFinancialAsync(vendorId);
        return Ok(result);
    }

    [HttpGet("{vendorId:long}/history")]
    public async Task<IActionResult> GetPayoutHistory(long vendorId)
    {
        var result = await _adminVendorPayoutService.ListPayoutsAsync(vendorId);
        return Ok(result);
    }

    [HttpPost("{vendorId:long}")]
    public async Task<IActionResult> RecordPayout(long vendorId, [FromBody] RecordVendorPayoutDto dto)
    {
        var adminId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _adminVendorPayoutService.RecordPayoutAsync(vendorId, adminId, dto);
        return Ok(result);
    }
}
