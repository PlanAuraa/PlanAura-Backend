using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services;
using Planura.Shared.Errors.Models;

namespace Planura.Apis.Controllers;

[ApiController]
[Route("api/ai-chat")]
[Authorize(Policy = AuthorizationPolicies.ClientOnly)]
public class AiChatController : ControllerBase
{
    private readonly IAiChatService _aiChatService;
    private readonly ICurrentUserService _currentUserService;

    public AiChatController(IAiChatService aiChatService, ICurrentUserService currentUserService)
    {
        _aiChatService = aiChatService;
        _currentUserService = currentUserService;
    }

    private long CurrentUserId => _currentUserService.UserId
        ?? throw new UnAuthorizedExeption("No authenticated user.");

    [HttpPost("messages")]
    public async Task<ActionResult<ChatMessageResponseDto>> SendMessage([FromBody] SendChatMessageDto dto)
    {
        var result = await _aiChatService.SendMessageAsync(CurrentUserId, dto);
        return Ok(result);
    }
}
