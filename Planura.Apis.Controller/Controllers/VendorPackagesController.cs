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

public class VendorPackagesController : ControllerBase
{
    private readonly IVendorPackageService _service;
    private readonly ICurrentUserService _currentUserService;

    public VendorPackagesController(IVendorPackageService service, ICurrentUserService currentUserService)
    {
        _service = service;
        _currentUserService = currentUserService;
    }

    // Public marketplace reads — packages are shown on public vendor profiles /
    // explore pages, so guests need to be able to list/search them without an
    // account. Mutations below keep the ApprovedVendor policy unchanged.
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<VendorPackageDto>>> GetAll([FromQuery] bool activeOnly = false)
    {
        var result = await _service.GetAllAsync(activeOnly);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<VendorPackageDto>> GetById(long id)
    {
        return Ok(await _service.GetByIdAsync(id));
    }

    [AllowAnonymous]
    [HttpGet("by-vendor/{vendorId:long}")]
    public async Task<ActionResult<IEnumerable<VendorPackageDto>>> GetByVendor(long vendorId, [FromQuery] bool activeOnly = false)
    {
        var result = await _service.GetByVendorAsync(vendorId, activeOnly);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<VendorPackageDto>>> Search([FromQuery] VendorPackageSearchDto query)
    {
        var result = await _service.SearchAsync(query);
        return Ok(result);
    }

    [Authorize(Policy = AuthorizationPolicies.ApprovedVendor)]
    [HttpPost]
    public async Task<ActionResult<VendorPackageDto>> Create([FromBody] CreateVendorPackageDto dto)
    {
        var vendorId = _currentUserService.VendorId
            ?? throw new UnAuthorizedExeption("No vendor profile is associated with this account.");

        var created = await _service.CreateAsync(vendorId, dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Policy = AuthorizationPolicies.ApprovedVendor)]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<VendorPackageDto>> Update(long id, [FromBody] UpdateVendorPackageDto dto)
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
}
