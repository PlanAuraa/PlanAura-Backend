using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services;

namespace Planura.Apis.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServiceCategoriesController : ControllerBase
{
    private readonly IServiceCategoryService _service;

    public ServiceCategoriesController(IServiceCategoryService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ServiceCategoryDto>>> GetAll([FromQuery] bool activeOnly = false)
    {
        var result = await _service.GetAllAsync(activeOnly);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ServiceCategoryDto>> GetById(long id)
    {
        return Ok(await _service.GetByIdAsync(id));
    }

    [HttpGet("by-slug/{slug}")]
    public async Task<ActionResult<ServiceCategoryDto>> GetBySlug(string slug)
    {
        return Ok(await _service.GetBySlugAsync(slug));
    }

    [HttpPost]
    public async Task<ActionResult<ServiceCategoryDto>> Create([FromBody] CreateServiceCategoryDto dto)
    {
        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ServiceCategoryDto>> Update(long id, [FromBody] UpdateServiceCategoryDto dto)
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
