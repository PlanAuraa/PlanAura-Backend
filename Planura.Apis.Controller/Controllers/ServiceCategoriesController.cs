using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services.CategoryService;

namespace Planura.Apis.Controllers;

/// <summary>
/// Reads are public (matches the other public browse endpoints, e.g. vendor browse); mutations
/// are AdminOnly. Previously the whole controller was only <c>[Authorize]</c> (any authenticated
/// role), which let any logged-in client or vendor create/edit/delete platform categories - see
/// AdminDashboardPlan.md Section 1.3 / 5.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class ServiceCategoriesController : ControllerBase
{
    private readonly IServiceCategoryService _service;

    public ServiceCategoriesController(IServiceCategoryService service)
    {
        _service = service;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ServiceCategoryDto>>> GetAll([FromQuery] bool activeOnly = false)
    {
        var result = await _service.GetAllAsync(activeOnly);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ServiceCategoryDto>> GetById(long id)
    {
        return Ok(await _service.GetByIdAsync(id));
    }

    [AllowAnonymous]
    [HttpGet("by-slug/{slug}")]
    public async Task<ActionResult<ServiceCategoryDto>> GetBySlug(string slug)
    {
        return Ok(await _service.GetBySlugAsync(slug));
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ServiceCategoryDto>> Create([FromForm] CreateServiceCategoryDto dto)
    {
        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ServiceCategoryDto>> Update(long id, [FromForm] UpdateServiceCategoryDto dto)
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
