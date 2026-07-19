using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Services.AdminReport;

namespace Planura.Apis.Controllers;

/// <summary>
/// Analytics/reporting endpoints (AdminDashboardPlan.md Section 3 / 2.11) - a practical subset of
/// the plan's 26 charts. See AdminReportDtos.cs for what's covered vs. deferred.
/// </summary>
[ApiController]
[Route("api/admin/reports")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class AdminReportsController : ControllerBase
{
    private readonly IAdminReportService _adminReportService;

    public AdminReportsController(IAdminReportService adminReportService)
    {
        _adminReportService = adminReportService;
    }

    [HttpGet("users/registrations")]
    public async Task<IActionResult> GetUserRegistrations([FromQuery] int months = 12)
    {
        return Ok(await _adminReportService.GetUserRegistrationsAsync(months));
    }

    [HttpGet("bookings/monthly")]
    public async Task<IActionResult> GetBookingsMonthly([FromQuery] int months = 12)
    {
        return Ok(await _adminReportService.GetBookingsMonthlyAsync(months));
    }

    [HttpGet("payments/revenue")]
    public async Task<IActionResult> GetRevenueMonthly([FromQuery] int months = 12)
    {
        return Ok(await _adminReportService.GetRevenueMonthlyAsync(months));
    }

    [HttpGet("vendors/top")]
    public async Task<IActionResult> GetTopVendors([FromQuery] string? by = "revenue", [FromQuery] int take = 10)
    {
        return Ok(await _adminReportService.GetTopVendorsAsync(by, take));
    }

    [HttpGet("categories/top")]
    public async Task<IActionResult> GetTopCategories([FromQuery] string? by = "vendors", [FromQuery] int take = 10)
    {
        return Ok(await _adminReportService.GetTopCategoriesAsync(by, take));
    }

    [HttpGet("vendors/funnel")]
    public async Task<IActionResult> GetVendorFunnel()
    {
        return Ok(await _adminReportService.GetVendorVerificationFunnelAsync());
    }
}
