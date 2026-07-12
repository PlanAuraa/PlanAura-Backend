using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models.VendorVerification;
using Planura.Core.Application.Services;
using Planura.Shared.Errors.Models;

namespace Planura.Apis.Controllers;

[ApiController]
[Route("api/vendor-verifications")]
[Authorize(Policy = AuthorizationPolicies.VendorOnly)]
public class VendorVerificationController : ControllerBase
{
    private readonly IVendorVerificationService _vendorVerificationService;
    private readonly ICurrentUserService _currentUserService;

    public VendorVerificationController(
        IVendorVerificationService vendorVerificationService,
        ICurrentUserService currentUserService)
    {
        _vendorVerificationService = vendorVerificationService;
        _currentUserService = currentUserService;
    }

    [HttpGet("me/history")]
    public async Task<IActionResult> GetMyHistory()
    {
        var vendorId = _currentUserService.VendorId
            ?? throw new UnAuthorizedExeption("No vendor profile is associated with this account.");

        var result = await _vendorVerificationService.GetVerificationHistoryAsync(vendorId);

        return Ok(result);
    }

    [HttpPost("me/resubmit")]
    public async Task<IActionResult> Resubmit([FromForm] ResubmitVerificationDto dto)
    {
        var vendorId = _currentUserService.VendorId
            ?? throw new UnAuthorizedExeption("No vendor profile is associated with this account.");

        var result = await _vendorVerificationService.ResubmitVerificationAsync(vendorId, dto);

        return Ok(result);
    }
}
