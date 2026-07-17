namespace Planura.Core.Domain.Entities;

public class AiChatMessage
{
    public long Id { get; set; }
    public long ConversationId { get; set; }
    public string Role { get; set; } = null!;
    public string Content { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public AiChatConversation Conversation { get; set; } = null!;
}
