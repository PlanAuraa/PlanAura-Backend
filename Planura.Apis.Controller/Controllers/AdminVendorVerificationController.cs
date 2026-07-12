using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models.VendorVerification;
using Planura.Core.Application.Services;
using Planura.Core.Domain.Constants;

namespace Planura.Apis.Controllers;

[ApiController]
[Route("api/admin/vendor-verifications")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class AdminVendorVerificationController : ControllerBase
{
    private readonly IVendorVerificationService _vendorVerificationService;

    public AdminVendorVerificationController(
        IVendorVerificationService vendorVerificationService)
    {
        _vendorVerificationService = vendorVerificationService;
    }

    [HttpPost("approve")]
    public async Task<IActionResult> Approve([FromBody] ApproveVendorDto dto)
    {
        await _vendorVerificationService.ApproveVendorAsync(dto.VendorId);

        return Ok(new
        {
            Message = "Vendor approved successfully."
        });
    }

    [HttpPost("reject")]
    public async Task<IActionResult> Reject([FromBody] RejectVendorDto dto)
    {
        await _vendorVerificationService
            .RejectVendorAsync(dto.VendorId, dto.RejectionReason);

        return Ok(new
        {
            Message = "Vendor rejected successfully."
        });
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        var result = await _vendorVerificationService.GetPendingVendorsAsync();

        return Ok(result);
    }

    [HttpGet("{vendorId:long}")]
    public async Task<IActionResult> GetDetails(long vendorId)
    {
        var result = await _vendorVerificationService.GetVendorDetailsAsync(vendorId);

        return Ok(result);
    }

    [HttpGet("{vendorId:long}/history")]
    public async Task<IActionResult> GetHistory(long vendorId)
    {
        var result = await _vendorVerificationService.GetVerificationHistoryAsync(vendorId);

        return Ok(result);
    }
}