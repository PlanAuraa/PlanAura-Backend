using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Apis.Controller.Models;
using Planura.Core.Application.Abstraction.Authentication;
using Planura.Core.Application.Abstraction.Authentication.Contracts;
using Planura.Core.Application.Abstraction.Storage;
using Planura.Core.Application.Abstraction.Vendors;
using Planura.Core.Application.Abstraction.Vendors.Contracts;
using Planura.Shared.Constants;

namespace Planura.Apis.Controller.Controllers.Vendors
{
    [ApiController]
    [Route("api/vendors")]
    public class VendorsController : ControllerBase
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly IVendorDocumentAccessService _vendorDocumentAccessService;
        private readonly IVendorOnboardingService _vendorOnboardingService;
        private readonly IVendorVerificationService _vendorVerificationService;

        public VendorsController(
            ICurrentUserService currentUserService,
            IVendorDocumentAccessService vendorDocumentAccessService,
            IVendorOnboardingService vendorOnboardingService,
            IVendorVerificationService vendorVerificationService)
        {
            _currentUserService = currentUserService;
            _vendorDocumentAccessService = vendorDocumentAccessService;
            _vendorOnboardingService = vendorOnboardingService;
            _vendorVerificationService = vendorVerificationService;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Register([FromForm] VendorRegisterForm form, CancellationToken ct)
        {
            var request = form.ToRequest();
            var result = await _vendorOnboardingService.RegisterAsync(request, GetClientIp(), ct);
            return Ok(result);
        }

        [HttpGet("me/status")]
        [Authorize(Roles = Roles.Vendor)]
        public async Task<ActionResult<VendorStatusResponse>> GetMyStatus(CancellationToken ct)
        {
            var userId = _currentUserService.UserId!.Value;
            var result = await _vendorVerificationService.GetMyStatusAsync(userId, ct);
            return Ok(result);
        }

        [HttpGet("me/verification-history")]
        [Authorize(Roles = Roles.Vendor)]
        public async Task<ActionResult<VerificationHistoryDto>> GetMyVerificationHistory(CancellationToken ct)
        {
            var userId = _currentUserService.UserId!.Value;
            var result = await _vendorVerificationService.GetMyHistoryAsync(userId, ct);
            return Ok(result);
        }

        [HttpPost("me/resubmit")]
        [Authorize(Roles = Roles.Vendor)]
        public async Task<ActionResult<VendorStatusResponse>> Resubmit([FromForm] VendorResubmitForm form, CancellationToken ct)
        {
            var userId = _currentUserService.UserId!.Value;
            var request = form.ToRequest();
            var result = await _vendorVerificationService.ResubmitAsync(userId, request, ct);
            return Ok(result);
        }

        // Placeholder proving the ApprovedVendor policy is enforced server-side. Real dashboard
        // data (packages, availability) is wired in a later phase.
        [HttpGet("me/dashboard")]
        [Authorize(Policy = AuthorizationPolicies.ApprovedVendor)]
        public IActionResult Dashboard()
        {
            return Ok(new { message = "Vendor dashboard placeholder — ApprovedVendor policy enforced." });
        }

        [HttpGet("me/documents/{documentId:guid}")]
        [Authorize]
        public async Task<IActionResult> GetDocument(Guid documentId, CancellationToken ct)
        {
            var userId = _currentUserService.UserId!.Value;
            var isAdmin = _currentUserService.IsInRole(Roles.Admin);

            var result = await _vendorDocumentAccessService.GetDocumentStreamAsync(documentId, userId, isAdmin, ct);
            return File(result.Stream, result.ContentType, result.FileName);
        }

        private string? GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
