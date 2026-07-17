namespace Planura.Core.Application.Models;

public class ChatMessageResponseDto
{
    public long ConversationId { get; set; }
    public AiChatMessageDto UserMessage { get; set; } = null!;
    public AiChatMessageDto AssistantMessage { get; set; } = null!;
}
