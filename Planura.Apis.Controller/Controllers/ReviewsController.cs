using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services;
using Planura.Shared.Errors.Models;

namespace Planura.Apis.Controllers;

[ApiController]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly ICurrentUserService _currentUserService;

    public ReviewsController(IReviewService reviewService, ICurrentUserService currentUserService)
    {
        _reviewService = reviewService;
        _currentUserService = currentUserService;
    }

    private long CurrentUserId => _currentUserService.UserId
        ?? throw new UnAuthorizedExeption("No authenticated user.");

    private long CurrentVendorId => _currentUserService.VendorId
        ?? throw new UnAuthorizedExeption("No vendor profile is associated with this account.");

    /// <summary>Public list of a vendor's reviews (for the vendor's public profile).</summary>
    [AllowAnonymous]
    [HttpGet("vendor/{vendorId:long}")]
    public async Task<ActionResult<PagedResult<ReviewDto>>> GetVendorReviews(
        long vendorId, [FromQuery] ReviewFilterDto filter)
    {
        return Ok(await _reviewService.GetVendorReviewsAsync(vendorId, filter));
    }

    /// <summary>Public rating summary (average + star breakdown) for a vendor.</summary>
    [AllowAnonymous]
    [HttpGet("vendor/{vendorId:long}/summary")]
    public async Task<ActionResult<ReviewSummaryDto>> GetVendorSummary(long vendorId)
    {
        return Ok(await _reviewService.GetVendorSummaryAsync(vendorId));
    }

    /// <summary>The reviews clients left for the logged-in vendor.</summary>
    [Authorize(Policy = AuthorizationPolicies.VendorOnly)]
    [HttpGet("incoming")]
    public async Task<ActionResult<PagedResult<ReviewDto>>> GetMyReviews([FromQuery] ReviewFilterDto filter)
    {
        return Ok(await _reviewService.GetVendorReviewsAsync(CurrentVendorId, filter));
    }

    /// <summary>The logged-in vendor's own rating summary.</summary>
    [Authorize(Policy = AuthorizationPolicies.VendorOnly)]
    [HttpGet("incoming/summary")]
    public async Task<ActionResult<ReviewSummaryDto>> GetMySummary()
    {
        return Ok(await _reviewService.GetVendorSummaryAsync(CurrentVendorId));
    }

    /// <summary>A client leaves a review for a booking they made.</summary>
    [Authorize(Policy = AuthorizationPolicies.ClientOnly)]
    [HttpPost]
    public async Task<ActionResult<ReviewDto>> Create([FromBody] CreateReviewDto dto)
    {
        return Ok(await _reviewService.CreateReviewAsync(CurrentUserId, dto));
    }

    /// <summary>The vendor replies (once) to a review left for them.</summary>
    [Authorize(Policy = AuthorizationPolicies.VendorOnly)]
    [HttpPost("{reviewId:long}/response")]
    public async Task<ActionResult<ReviewDto>> Respond(long reviewId, [FromBody] RespondToReviewDto dto)
    {
        return Ok(await _reviewService.RespondAsync(CurrentVendorId, reviewId, dto));
    }
}
