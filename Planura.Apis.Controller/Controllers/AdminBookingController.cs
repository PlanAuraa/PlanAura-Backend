using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models;
using Planura.Core.Application.Models.AdminBooking;
using Planura.Core.Application.Services.AdminBooking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Planura.Apis.Controller.Controllers
{
    [ApiController]
    [Route("api/admin/bookings")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public class AdminBookingController : ControllerBase    
    {
        private readonly IAdminBookingService _adminBookingService;

        public AdminBookingController(IAdminBookingService adminBookingService)
        {
            _adminBookingService = adminBookingService;
        }
        [HttpGet("disputes")]
        public async Task<IActionResult> GetOpenDisputesAsync()
        {
            var disputes = await _adminBookingService.GetOpenDisputesAsync();
            return Ok(disputes);
        }

        [HttpGet("disputes/{bookingId:long}")]
        public async Task<ActionResult<AdminDisputeDetailsDto>> GetDisputeDetailsAsync(long bookingId)
        {
            var dispute = await _adminBookingService.GetDisputeDetailsAsync(bookingId);
            return Ok(dispute);
        }

        [HttpPost]
        [Route("disputes/{bookingId}/resolve")]
        public async Task<IActionResult> ResolveDisputeAsync(long bookingId, [FromBody] ResolveDisputeDto dto)
        {
            var adminId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
             await _adminBookingService.ResolveDisputeAsync(
    bookingId,
    adminId,
    dto);
            return NoContent();
        }
        [HttpGet]
        public async Task<ActionResult<PagedResult<AdminBookingDto>>> GetBookings(
    [FromQuery] AdminBookingFilterDto filter)
        {
            var result = await _adminBookingService.GetBookingsAsync(filter);
            return Ok(result);
        }

        [HttpGet("{bookingId:long}/payment-detail")]
        public async Task<ActionResult<AdminBookingPaymentDetailDto>> GetPaymentDetail(long bookingId)
        {
            var result = await _adminBookingService.GetBookingPaymentDetailAsync(bookingId);
            return Ok(result);
        }

        [HttpGet("cancellation-requests")]
        public async Task<ActionResult<IEnumerable<CancellationRequestListItemDto>>> GetCancellationRequests()
        {
            var result = await _adminBookingService.GetCancellationRequestsAsync();
            return Ok(result);
        }

        [HttpPost("{bookingId:long}/approve-cancellation")]
        public async Task<ActionResult<AdminBookingDto>> ApproveCancellation(
            long bookingId, [FromBody] ApproveCancellationDto dto)
        {
            var adminId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _adminBookingService.ApproveCancellationAsync(bookingId, adminId, dto);
            return Ok(result);
        }

        [HttpPost("{bookingId:long}/reject-cancellation")]
        public async Task<ActionResult<AdminBookingDto>> RejectCancellation(
            long bookingId, [FromBody] RejectCancellationDto dto)
        {
            var adminId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _adminBookingService.RejectCancellationAsync(bookingId, adminId, dto);
            return Ok(result);
        }

    }


}
