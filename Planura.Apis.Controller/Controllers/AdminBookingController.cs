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

    }


}
