using Planura.Core.Application.Models;

namespace Planura.Core.Application.Services;

public interface IEventPlanService
{
    Task<EventPlanDto> CreateEventPlanAsync(long clientUserId, CreateEventPlanDto dto);
    Task<IEnumerable<EventPlanDto>> ListMyEventPlansAsync(long clientUserId);
    Task<EventPlanDto> GetEventPlanAsync(long eventPlanId, long clientUserId);
    Task<EventPlanDto> UpdateEventPlanAsync(long eventPlanId, long clientUserId, UpdateEventPlanDto dto);
    Task DeleteEventPlanAsync(long eventPlanId, long clientUserId);
    Task<EventPlanChecklistItemDto> AddChecklistItemAsync(long eventPlanId, long clientUserId, AddChecklistItemDto dto);
    Task RemoveChecklistItemAsync(long eventPlanId, long clientUserId, long serviceCategoryId);
}
