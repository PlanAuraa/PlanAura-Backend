using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Abstraction.Authentication;
using Planura.Core.Application.Abstraction.Vendors;
using Planura.Core.Application.Abstraction.Vendors.Contracts;
using Planura.Shared.Constants;

namespace Planura.Apis.Controller.Controllers.Vendors
{
    [ApiController]
    [Route("api/admin/verifications")]
    [Authorize(Roles = Roles.Admin)]
    public class AdminVerificationController : ControllerBase
    {
        private readonly IAdminVerificationService _adminVerificationService;
        private readonly ICurrentUserService _currentUserService;

        public AdminVerificationController(IAdminVerificationService adminVerificationService, ICurrentUserService currentUserService)
        {
            _adminVerificationService = adminVerificationService;
            _currentUserService = currentUserService;
        }

        [HttpGet("pending")]
        public async Task<ActionResult<PagedResult<VendorApplicationSummaryDto>>> GetPending(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            var result = await _adminVerificationService.GetPendingAsync(page, pageSize, ct);
            return Ok(result);
        }

        [HttpGet("{requestId:guid}")]
        public async Task<ActionResult<VendorApplicationDetailsDto>> GetDetails(Guid requestId, CancellationToken ct)
        {
            var result = await _adminVerificationService.GetDetailsAsync(requestId, ct);
            return Ok(result);
        }

        [HttpPost("{requestId:guid}/approve")]
        public async Task<ActionResult<VendorStatusResponse>> Approve(Guid requestId, CancellationToken ct)
        {
            var adminUserId = _currentUserService.UserId!.Value;
            var result = await _adminVerificationService.ApproveAsync(requestId, adminUserId, ct);
            return Ok(result);
        }

        [HttpPost("{requestId:guid}/reject")]
        public async Task<ActionResult<VendorStatusResponse>> Reject(Guid requestId, RejectApplicationRequest request, CancellationToken ct)
        {
            var adminUserId = _currentUserService.UserId!.Value;
            var result = await _adminVerificationService.RejectAsync(requestId, adminUserId, request, ct);
            return Ok(result);
        }

        [HttpGet("vendors/{vendorId:guid}/history")]
        public async Task<ActionResult<VerificationHistoryDto>> GetVendorHistory(Guid vendorId, CancellationToken ct)
        {
            var result = await _adminVerificationService.GetVendorHistoryAsync(vendorId, ct);
            return Ok(result);
        }
    }
}
