namespace Planura.Core.Application.Models.AiInvitation;

// Bound from a JSON body on AiController.GenerateInvitation - the event plan
// the invitation belongs to, plus a theme and the client's freeform styling
// instructions for the invitation card.
public class GenerateInvitationDto
{
    public long EventPlanId { get; set; }
    public string Theme { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
}
