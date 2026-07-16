using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services;
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

    [HttpPost("{id:long}/accept")]
    public async Task<ActionResult<BookingRequestDto>> Accept(long id)
    {
        var result = await _bookingService.AcceptBookingRequestAsync(id, CurrentUserId);
        return Ok(result);
    }

    [HttpPost("{id:long}/reject")]
    public async Task<ActionResult<BookingRequestDto>> Reject(long id, [FromBody] RejectBookingRequestDto dto)
    {
        var result = await _bookingService.RejectBookingRequestAsync(id, CurrentUserId, dto.Reason);
        return Ok(result);
    }
}
