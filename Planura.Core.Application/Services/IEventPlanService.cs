using Planura.Core.Application.Models;

namespace Planura.Core.Application.Services;

public interface IEventPlanService
{
    Task<EventPlanDto> CreateEventPlanAsync(long clientUserId, CreateEventPlanDto dto);
    Task<IEnumerable<EventPlanDto>> ListMyEventPlansAsync(long clientUserId);
    Task<EventPlanDto> GetEventPlanAsync(long eventPlanId, long clientUserId);
    Task DeleteEventPlanAsync(long eventPlanId, long clientUserId);
}
