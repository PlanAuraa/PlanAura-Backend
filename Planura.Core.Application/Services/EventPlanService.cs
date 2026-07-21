using AutoMapper;
using Planura.Core.Application.Models;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services;

public class EventPlanService : IEventPlanService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public EventPlanService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<EventPlanDto> CreateEventPlanAsync(long clientUserId, CreateEventPlanDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.EventType))
        {
            throw new BadRequestExeption("Event type is required.");
        }

        var clientId = await ResolveClientIdAsync(clientUserId);

        var eventPlan = new EventPlan
        {
            ClientId = clientId,
            Title = dto.Title,
            EventType = dto.EventType,
            EventDate = dto.EventDate,
            City = dto.City,
            GuestCount = dto.GuestCount,
            BudgetTotal = dto.BudgetTotal,
            StyleNotes = dto.StyleNotes
        };

        await _unitOfWork.Repository<EventPlan, long>().AddAsync(eventPlan);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<EventPlanDto>(eventPlan);
    }

    public async Task<IEnumerable<EventPlanDto>> ListMyEventPlansAsync(long clientUserId)
    {
        var clientId = await ResolveClientIdAsync(clientUserId);

        var plans = await _unitOfWork.Repository<EventPlan, long>()
            .GetAllWithSpecAsync(new EventPlansByClientSpecification(clientId));

        return _mapper.Map<IEnumerable<EventPlanDto>>(plans);
    }

    public async Task<EventPlanDto> GetEventPlanAsync(long eventPlanId, long clientUserId)
    {
        var clientId = await ResolveClientIdAsync(clientUserId);

        var eventPlan = await _unitOfWork.Repository<EventPlan, long>().GetAsync(eventPlanId);
        if (eventPlan is null || eventPlan.ClientId != clientId)
        {
            throw new NotFoundExeption(nameof(EventPlan), eventPlanId);
        }

        return _mapper.Map<EventPlanDto>(eventPlan);
    }

    public async Task<EventPlanDto> UpdateEventPlanAsync(long eventPlanId, long clientUserId, UpdateEventPlanDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.EventType))
        {
            throw new BadRequestExeption("Event type is required.");
        }

        var clientId = await ResolveClientIdAsync(clientUserId);

        var repo = _unitOfWork.Repository<EventPlan, long>();
        var eventPlan = await repo.GetAsync(eventPlanId);
        if (eventPlan is null || eventPlan.ClientId != clientId)
        {
            throw new NotFoundExeption(nameof(EventPlan), eventPlanId);
        }

        eventPlan.Title = dto.Title;
        eventPlan.EventType = dto.EventType;
        eventPlan.EventDate = dto.EventDate;
        eventPlan.City = dto.City;
        eventPlan.GuestCount = dto.GuestCount;
        eventPlan.BudgetTotal = dto.BudgetTotal;
        eventPlan.StyleNotes = dto.StyleNotes;
        eventPlan.UpdatedAt = DateTimeOffset.UtcNow;

        repo.Update(eventPlan);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<EventPlanDto>(eventPlan);
    }

    public async Task DeleteEventPlanAsync(long eventPlanId, long clientUserId)
    {
        var clientId = await ResolveClientIdAsync(clientUserId);

        var repo = _unitOfWork.Repository<EventPlan, long>();
        var eventPlan = await repo.GetAsync(eventPlanId);
        if (eventPlan is null || eventPlan.ClientId != clientId)
        {
            throw new NotFoundExeption(nameof(EventPlan), eventPlanId);
        }

        var bookingCount = await _unitOfWork.Repository<BookingRequest, long>()
            .GetCountAsync(new BookingRequestsByEventPlanSpecification(eventPlanId));

        if (bookingCount > 0)
        {
            throw new BadRequestExeption(
                "Cannot delete an event plan that has booking requests linked to it.");
        }

        repo.Delete(eventPlan);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<long> ResolveClientIdAsync(long userId)
    {
        var client = await _unitOfWork.Repository<Client, long>()
            .GetWithSpecAsync(new ClientByUserIdSpecification(userId));

        if (client is null)
        {
            throw new NotFoundExeption(nameof(Client), userId);
        }

        return client.Id;
    }
}
