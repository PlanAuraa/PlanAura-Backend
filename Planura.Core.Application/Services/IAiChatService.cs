using Planura.Core.Application.Models;

namespace Planura.Core.Application.Services;

public interface IAiChatService
{
    Task<ChatMessageResponseDto> SendMessageAsync(long clientUserId, SendChatMessageDto dto);
}
