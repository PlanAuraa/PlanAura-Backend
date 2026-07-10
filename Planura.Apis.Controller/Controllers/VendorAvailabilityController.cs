using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services;

namespace Planura.Apis.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VendorAvailabilityController : ControllerBase
{
    private readonly IVendorAvailabilityService _service;

    public VendorAvailabilityController(IVendorAvailabilityService service)
    {
        _service = service;
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

    [HttpPost]
    public async Task<ActionResult<VendorAvailabilityDto>> Create([FromBody] CreateVendorAvailabilityDto dto)
    {
        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<VendorAvailabilityDto>> Update(long id, [FromBody] UpdateVendorAvailabilityDto dto)
    {
        return Ok(await _service.UpdateAsync(id, dto));
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
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
