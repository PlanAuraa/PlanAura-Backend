using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services;

namespace Planura.Apis.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]

public class VendorPackagesController : ControllerBase
{
    private readonly IVendorPackageService _service;

    public VendorPackagesController(IVendorPackageService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<VendorPackageDto>>> GetAll([FromQuery] bool activeOnly = false)
    {
        var result = await _service.GetAllAsync(activeOnly);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<VendorPackageDto>> GetById(long id)
    {
        return Ok(await _service.GetByIdAsync(id));
    }

    [HttpGet("by-vendor/{vendorId:long}")]
    public async Task<ActionResult<IEnumerable<VendorPackageDto>>> GetByVendor(long vendorId, [FromQuery] bool activeOnly = false)
    {
        var result = await _service.GetByVendorAsync(vendorId, activeOnly);
        return Ok(result);
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<VendorPackageDto>>> Search([FromQuery] VendorPackageSearchDto query)
    {
        var result = await _service.SearchAsync(query);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<VendorPackageDto>> Create([FromBody] CreateVendorPackageDto dto)
    {
        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<VendorPackageDto>> Update(long id, [FromBody] UpdateVendorPackageDto dto)
    {
        return Ok(await _service.UpdateAsync(id, dto));
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
