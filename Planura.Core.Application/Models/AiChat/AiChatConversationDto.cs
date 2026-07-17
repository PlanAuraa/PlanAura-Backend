namespace Planura.Core.Application.Models;

public class AiChatConversationDto
{
    public long Id { get; set; }
    public long? EventPlanId { get; set; }
    public string? Title { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public IEnumerable<AiChatMessageDto> Messages { get; set; } = new List<AiChatMessageDto>();
}
