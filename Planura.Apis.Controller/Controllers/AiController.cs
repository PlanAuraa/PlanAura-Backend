using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models.AiInvitation;
using Planura.Core.Application.Models.AiVisualizer;
using Planura.Core.Application.Services;
using Planura.Core.Application.Services.AiInvitation;
using Planura.Core.Application.Services.AiVisualizer;
using Planura.Shared.Errors.Models;

namespace Planura.Apis.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize(Policy = AuthorizationPolicies.ClientOnly)]
public class AiController : ControllerBase
{
    private readonly IAiVisualizerService _aiVisualizerService;
    private readonly IAiInvitationService _aiInvitationService;
    private readonly ICurrentUserService _currentUserService;

    public AiController(
        IAiVisualizerService aiVisualizerService,
        IAiInvitationService aiInvitationService,
        ICurrentUserService currentUserService)
    {
        _aiVisualizerService = aiVisualizerService;
        _aiInvitationService = aiInvitationService;
        _currentUserService = currentUserService;
    }

    private long CurrentUserId => _currentUserService.UserId
        ?? throw new UnAuthorizedExeption("No authenticated user.");

    /// <summary>
    /// Uploads a photo of an empty venue plus a styling prompt and returns a
    /// URL to an AI-generated visualization of the styled space
    /// (black-forest-labs/FLUX.1-Kontext-dev via Hugging Face).
    /// </summary>
    [HttpPost("visualize")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<VisualizeEventResponseDto>> Visualize([FromForm] VisualizeEventDto dto)
    {
        var result = await _aiVisualizerService.VisualizeEventAsync(CurrentUserId, dto);
        return Ok(result);
    }

    /// <summary>
    /// Generates an AI invitation card image for an event plan the current
    /// client owns, from a theme and a freeform styling prompt, and persists
    /// it as a new AiInvitation record.
    /// </summary>
    [HttpPost("invitations")]
    public async Task<ActionResult<InvitationDto>> GenerateInvitation([FromBody] GenerateInvitationDto dto)
    {
        var result = await _aiInvitationService.GenerateInvitationAsync(CurrentUserId, dto);
        return Ok(result);
    }

    /// <summary>
    /// Lists previously generated AI invitations for an event plan the
    /// current client owns, newest first.
    /// </summary>
    [HttpGet("invitations/{eventPlanId}")]
    public async Task<ActionResult<IEnumerable<InvitationDto>>> ListInvitations(long eventPlanId)
    {
        var result = await _aiInvitationService.ListInvitationsAsync(CurrentUserId, eventPlanId);
        return Ok(result);
    }
}
