using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services;
using Planura.Shared.Errors.Models;

namespace Planura.Apis.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]

public class VendorAvailabilityController : ControllerBase
{
    private readonly IVendorAvailabilityService _service;
    private readonly ICurrentUserService _currentUserService;

    public VendorAvailabilityController(IVendorAvailabilityService service, ICurrentUserService currentUserService)
    {
        _service = service;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<VendorAvailabilityDto>>> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<VendorAvailabilityDto>> GetById(long id)
    {
        return Ok(await _service.GetByIdAsync(id));
    }

    [HttpGet("by-vendor/{vendorId:long}")]
    public async Task<ActionResult<IEnumerable<VendorAvailabilityDto>>> GetByVendor(long vendorId)
    {
        var result = await _service.GetByVendorAsync(vendorId);
        return Ok(result);
    }

    [HttpPost("check")]
    public async Task<ActionResult<AvailabilityCheckResultDto>> CheckAvailability([FromBody] AvailabilityCheckDto dto)
    {
        var result = await _service.CheckAvailabilityAsync(dto);
        return Ok(result);
    }

    [Authorize(Policy = AuthorizationPolicies.ApprovedVendor)]
    [HttpPost]
    public async Task<ActionResult<VendorAvailabilityDto>> Create([FromBody] CreateVendorAvailabilityDto dto)
    {
        var vendorId = _currentUserService.VendorId
            ?? throw new UnAuthorizedExeption("No vendor profile is associated with this account.");

        var created = await _service.CreateAsync(vendorId, dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Policy = AuthorizationPolicies.ApprovedVendor)]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<VendorAvailabilityDto>> Update(long id, [FromBody] UpdateVendorAvailabilityDto dto)
    {
        var vendorId = _currentUserService.VendorId
            ?? throw new UnAuthorizedExeption("No vendor profile is associated with this account.");

        return Ok(await _service.UpdateAsync(id, vendorId, dto));
    }

    [Authorize(Policy = AuthorizationPolicies.ApprovedVendor)]
    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Delete(long id)
    {
        var vendorId = _currentUserService.VendorId
            ?? throw new UnAuthorizedExeption("No vendor profile is associated with this account.");

        await _service.DeleteAsync(id, vendorId);
        return NoContent();
    }

    [HttpPost("book")]
    public async Task<ActionResult<VendorAvailabilityDto>> BookSlot([FromBody] BookSlotDto dto)
    {
        var result = await _service.BookSlotAsync(dto);
        return Ok(result);
    }

    [HttpPost("{availabilityId:long}/cancel-booking")]
    public async Task<ActionResult<VendorAvailabilityDto>> CancelBooking(long availabilityId)
    {
        var result = await _service.CancelBookingAsync(availabilityId);
        return Ok(result);
    }
}
