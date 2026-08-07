using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class AiInvitationsByEventPlanSpecification : BaseSpecification<AiInvitation>
{
    public AiInvitationsByEventPlanSpecification(long eventPlanId)
        : base(invitation => invitation.EventPlanId == eventPlanId)
    {
        ApplyOrderByDescending(invitation => invitation.CreatedAt);
    }
}
