namespace Planura.Core.Domain.Entities;

public class AiInvitation
{
    public long Id { get; set; }
    public long EventPlanId { get; set; }
    public string? Theme { get; set; }
    public string? ImageUrl { get; set; }
    public string? Prompt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public EventPlan EventPlan { get; set; } = null!;
}
