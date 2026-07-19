using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services;
using Planura.Core.Application.Services.Booking;
using Planura.Shared.Errors.Models;

namespace Planura.Apis.Controllers;

[ApiController]
[Route("api/booking-requests")]
[Authorize(Policy = AuthorizationPolicies.ClientOnly)]
public class BookingRequestsController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly ICurrentUserService _currentUserService;

    public BookingRequestsController(IBookingService bookingService, ICurrentUserService currentUserService)
    {
        _bookingService = bookingService;
        _currentUserService = currentUserService;
    }

    private long CurrentUserId => _currentUserService.UserId
        ?? throw new UnAuthorizedExeption("No authenticated user.");

    [HttpPost]
    public async Task<ActionResult<BookingRequestDto>> Create([FromBody] CreateBookingRequestDto dto)
    {
        var result = await _bookingService.CreateBookingRequestAsync(CurrentUserId, dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<BookingRequestDto>>> List([FromQuery] BookingRequestFilterDto filter)
    {
        var result = await _bookingService.ListMyBookingRequestsAsync(CurrentUserId, filter);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<BookingRequestDto>> GetById(long id)
    {
        var result = await _bookingService.GetBookingRequestAsync(id, CurrentUserId);
        return Ok(result);
    }

    [HttpPatch("{id:long}/cancel")]
    public async Task<ActionResult<BookingRequestDto>> Cancel(long id)
    {
        var result = await _bookingService.CancelBookingRequestAsync(id, CurrentUserId);
        return Ok(result);
    }

    [HttpPost("{id:long}/dispute")]
    public async Task<ActionResult<BookingRequestDto>> Dispute(long id, [FromBody] FlagDisputeDto dto)
    {
        var result = await _bookingService.FlagDisputeAsync(id, CurrentUserId, dto.Reason);
        return Ok(result);
    }
}
