using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models;
using Planura.Core.Application.Models.AdminBooking;
using Planura.Core.Application.Services;
using Planura.Core.Application.Services.Booking;
using Planura.Shared.Errors.Models;

namespace Planura.Apis.Controllers;

[ApiController]
[Route("api/booking-requests")]
[Authorize(Policy = AuthorizationPolicies.VendorOnly)]
public class VendorBookingRequestsController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly ICurrentUserService _currentUserService;

    public VendorBookingRequestsController(IBookingService bookingService, ICurrentUserService currentUserService)
    {
        _bookingService = bookingService;
        _currentUserService = currentUserService;
    }

    private long CurrentUserId => _currentUserService.UserId
        ?? throw new UnAuthorizedExeption("No authenticated user.");

    [HttpGet("incoming")]
    public async Task<ActionResult<PagedResult<BookingRequestDto>>> ListIncoming([FromQuery] BookingRequestFilterDto filter)
    {
        var result = await _bookingService.ListVendorBookingRequestsAsync(CurrentUserId, filter);
        return Ok(result);
    }

    [HttpGet("incoming/{id:long}")]
    public async Task<ActionResult<BookingRequestDto>> GetIncomingById(long id)
    {
        var result = await _bookingService.GetVendorBookingRequestAsync(id, CurrentUserId);
        return Ok(result);
    }

    /// <summary>The permanent "Booking Activity" audit trail — same source of truth the client sees.</summary>
    [HttpGet("incoming/{id:long}/timeline")]
    public async Task<ActionResult<List<BookingStatusHistoryEntryDto>>> GetTimeline(long id)
    {
        var result = await _bookingService.GetVendorBookingTimelineAsync(id, CurrentUserId);
        return Ok(result);
    }

    /// <summary>
    /// Vendor-side "report a problem". Routed under incoming/ rather than mirroring the client's
    /// {id}/dispute because BookingRequestsController shares the api/booking-requests prefix - two
    /// actions on the same template would be an ambiguous match, since the ClientOnly/VendorOnly
    /// policies gate authorization, not routing.
    /// </summary>
    [HttpPost("incoming/{id:long}/dispute")]
    public async Task<ActionResult<BookingRequestDto>> Dispute(long id, [FromBody] FlagDisputeDto dto)
    {
        var result = await _bookingService.FlagDisputeAsync(id, CurrentUserId, dto.Reason);
        return Ok(result);
    }

    [HttpPost("{id:long}/accept")]
    public async Task<ActionResult<BookingRequestDto>> Accept(long id, [FromBody] AcceptBookingRequestDto dto)
    {
        var result = await _bookingService.AcceptBookingRequestAsync(id, CurrentUserId, dto.AgreementAccepted);
        return Ok(result);
    }

    [HttpPost("{id:long}/reject")]
    public async Task<ActionResult<BookingRequestDto>> Reject(long id, [FromBody] RejectBookingRequestDto dto)
    {
        var result = await _bookingService.RejectBookingRequestAsync(id, CurrentUserId, dto.Reason);
        return Ok(result);
    }
}
