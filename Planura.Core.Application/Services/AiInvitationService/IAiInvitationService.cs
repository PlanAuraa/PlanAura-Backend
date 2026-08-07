using Planura.Core.Application.Models.AiInvitation;

namespace Planura.Core.Application.Services.AiInvitation;

public interface IAiInvitationService
{
    Task<InvitationDto> GenerateInvitationAsync(long clientUserId, GenerateInvitationDto dto);
    Task<IEnumerable<InvitationDto>> ListInvitationsAsync(long clientUserId, long eventPlanId);
}
