using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services;
//using Planura.Core.Application.Services.EventPlanService;
using Planura.Shared.Errors.Models;

namespace Planura.Apis.Controllers;

[ApiController]
[Route("api/event-plans")]
[Authorize(Policy = AuthorizationPolicies.ClientOnly)]
public class EventPlansController : ControllerBase
{
    private readonly IEventPlanService _eventPlanService;
    private readonly ICurrentUserService _currentUserService;

    public EventPlansController(IEventPlanService eventPlanService, ICurrentUserService currentUserService)
    {
        _eventPlanService = eventPlanService;
        _currentUserService = currentUserService;
    }

    private long CurrentUserId => _currentUserService.UserId
        ?? throw new UnAuthorizedExeption("No authenticated user.");

    [HttpPost]
    public async Task<ActionResult<EventPlanDto>> Create([FromBody] CreateEventPlanDto dto)
    {
        var result = await _eventPlanService.CreateEventPlanAsync(CurrentUserId, dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EventPlanDto>>> List()
    {
        var result = await _eventPlanService.ListMyEventPlansAsync(CurrentUserId);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<EventPlanDto>> GetById(long id)
    {
        var result = await _eventPlanService.GetEventPlanAsync(id, CurrentUserId);
        return Ok(result);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<EventPlanDto>> Update(long id, [FromBody] UpdateEventPlanDto dto)
    {
        var result = await _eventPlanService.UpdateEventPlanAsync(id, CurrentUserId, dto);
        return Ok(result);
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Delete(long id)
    {
        await _eventPlanService.DeleteEventPlanAsync(id, CurrentUserId);
        return NoContent();
    }

    [HttpPost("{id:long}/checklist")]
    public async Task<ActionResult<EventPlanChecklistItemDto>> AddChecklistItem(long id, [FromBody] AddChecklistItemDto dto)
    {
        var result = await _eventPlanService.AddChecklistItemAsync(id, CurrentUserId, dto);
        return Ok(result);
    }

    [HttpDelete("{id:long}/checklist/{serviceCategoryId:long}")]
    public async Task<ActionResult> RemoveChecklistItem(long id, long serviceCategoryId)
    {
        await _eventPlanService.RemoveChecklistItemAsync(id, CurrentUserId, serviceCategoryId);
        return NoContent();
    }
}
