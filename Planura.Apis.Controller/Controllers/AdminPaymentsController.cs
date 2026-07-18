using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models.AdminPayment;
using Planura.Core.Application.Services.AdminPayment;
using System.Security.Claims;

namespace Planura.Apis.Controllers;

/// <summary>"Payments &amp; Transactions" admin surface (AdminDashboardPlan.md 2.9).</summary>
[ApiController]
[Route("api/admin/payments")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class AdminPaymentsController : ControllerBase
{
    private readonly IAdminPaymentService _adminPaymentService;

    public AdminPaymentsController(IAdminPaymentService adminPaymentService)
    {
        _adminPaymentService = adminPaymentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPayments([FromQuery] AdminPaymentFilterDto filter)
    {
        var result = await _adminPaymentService.ListPaymentsAsync(filter);
        return Ok(result);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var result = await _adminPaymentService.GetSummaryAsync();
        return Ok(result);
    }

    [HttpPost("{paymentId:long}/refund")]
    public async Task<IActionResult> Refund(long paymentId, [FromBody] RefundPaymentDto dto)
    {
        var adminId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _adminPaymentService.RefundPaymentAsync(paymentId, adminId, dto);
        return Ok(result);
    }
}
