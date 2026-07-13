using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class EventPlansByClientSpecification : BaseSpecification<EventPlan>
{
    public EventPlansByClientSpecification(long clientId)
        : base(plan => plan.ClientId == clientId)
    {
        ApplyOrderByDescending(plan => plan.CreatedAt);
    }
}
