namespace Planura.Core.Domain.Entities;

public class AiChatConversation
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    public long? EventPlanId { get; set; }
    public string? Title { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Client Client { get; set; } = null!;
    public EventPlan? EventPlan { get; set; }
    public ICollection<AiChatMessage> Messages { get; set; } = new List<AiChatMessage>();
}
