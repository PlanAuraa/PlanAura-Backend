namespace Planura.Core.Application.Models.AiInvitation;

public class InvitationDto
{
    public long Id { get; set; }
    public long EventPlanId { get; set; }
    public string Theme { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
