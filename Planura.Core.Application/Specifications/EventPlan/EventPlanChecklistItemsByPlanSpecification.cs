using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class EventPlanChecklistItemsByPlanSpecification : BaseSpecification<EventPlanChecklistItem>
{
    public EventPlanChecklistItemsByPlanSpecification(long eventPlanId)
        : base(item => item.EventPlanId == eventPlanId)
    {
        AddInclude(item => item.ServiceCategory);
    }
}
