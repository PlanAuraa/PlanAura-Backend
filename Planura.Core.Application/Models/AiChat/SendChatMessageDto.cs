namespace Planura.Core.Application.Models;

public class SendChatMessageDto
{
    public long? ConversationId { get; set; }
    public long? EventPlanId { get; set; }
    public string Message { get; set; } = null!;
}
