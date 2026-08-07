using AutoMapper;
using Planura.Core.Application.Models;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services;

public class EventPlanService : IEventPlanService
{
    /// <summary>Bookings still money-committed to this plan — used for TotalBookedCost. Excludes
    /// Rejected/Cancelled/Expired (funds released) but includes CancellationRequested (funds still
    /// captured pending the admin's decision).</summary>
    private static readonly BookingStatus[] ActiveBookingStatuses =
    [
        BookingStatus.Pending,
        BookingStatus.Accepted,
        BookingStatus.AwaitingConfirmation,
        BookingStatus.CancellationRequested,
        BookingStatus.Completed
    ];

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

        var dto = _mapper.Map<EventPlanDto>(eventPlan);

        var bookings = await _unitOfWork.Repository<BookingRequest, long>()
            .GetAllWithSpecAsync(new BookingRequestsByEventPlanSpecification(eventPlanId));
        var activeBookings = bookings.Where(b => ActiveBookingStatuses.Contains(b.Status)).ToList();

        dto.TotalBookedCost = activeBookings.Sum(b => b.AgreedPrice ?? 0m);
        dto.RemainingBudget = eventPlan.BudgetTotal is null ? null : eventPlan.BudgetTotal - dto.TotalBookedCost;

        var checklistItems = await _unitOfWork.Repository<EventPlanChecklistItem, long>()
            .GetAllWithSpecAsync(new EventPlanChecklistItemsByPlanSpecification(eventPlanId));
        var satisfiedCategoryIds = activeBookings
            .Where(b => b.Vendor?.CategoryId is not null)
            .Select(b => b.Vendor!.CategoryId!.Value)
            .ToHashSet();

        dto.Checklist = checklistItems.Select(item => new EventPlanChecklistItemDto
        {
            ServiceCategoryId = item.ServiceCategoryId,
            CategoryName = item.ServiceCategory.NameEn,
            IconUrl = item.ServiceCategory.IconUrl,
            IsSatisfied = satisfiedCategoryIds.Contains(item.ServiceCategoryId)
        }).ToList();

        return dto;
    }

    public async Task<EventPlanChecklistItemDto> AddChecklistItemAsync(long eventPlanId, long clientUserId, AddChecklistItemDto dto)
    {
        var clientId = await ResolveClientIdAsync(clientUserId);

        var eventPlan = await _unitOfWork.Repository<EventPlan, long>().GetAsync(eventPlanId);
        if (eventPlan is null || eventPlan.ClientId != clientId)
        {
            throw new NotFoundExeption(nameof(EventPlan), eventPlanId);
        }

        var category = await _unitOfWork.Repository<ServiceCategory, long>().GetAsync(dto.ServiceCategoryId);
        if (category is null)
        {
            throw new NotFoundExeption(nameof(ServiceCategory), dto.ServiceCategoryId);
        }

        var alreadyExists = _unitOfWork.Repository<EventPlanChecklistItem, long>()
            .GetQueryable()
            .Any(item => item.EventPlanId == eventPlanId && item.ServiceCategoryId == dto.ServiceCategoryId);
        if (alreadyExists)
        {
            throw new BadRequestExeption("This category is already on the checklist.");
        }

        var checklistItem = new EventPlanChecklistItem
        {
            EventPlanId = eventPlanId,
            ServiceCategoryId = dto.ServiceCategoryId
        };
        await _unitOfWork.Repository<EventPlanChecklistItem, long>().AddAsync(checklistItem);
        await _unitOfWork.SaveChangesAsync();

        // Freshly added — the plan can't already have a booking satisfying a category the client
        // just decided they need, so IsSatisfied is always false at creation time.
        return new EventPlanChecklistItemDto
        {
            ServiceCategoryId = category.Id,
            CategoryName = category.NameEn,
            IconUrl = category.IconUrl,
            IsSatisfied = false
        };
    }

    public async Task RemoveChecklistItemAsync(long eventPlanId, long clientUserId, long serviceCategoryId)
    {
        var clientId = await ResolveClientIdAsync(clientUserId);

        var eventPlan = await _unitOfWork.Repository<EventPlan, long>().GetAsync(eventPlanId);
        if (eventPlan is null || eventPlan.ClientId != clientId)
        {
            throw new NotFoundExeption(nameof(EventPlan), eventPlanId);
        }

        var repo = _unitOfWork.Repository<EventPlanChecklistItem, long>();
        var checklistItem = repo.GetQueryable()
            .FirstOrDefault(item => item.EventPlanId == eventPlanId && item.ServiceCategoryId == serviceCategoryId);
        if (checklistItem is null)
        {
            throw new NotFoundExeption(nameof(EventPlanChecklistItem), serviceCategoryId);
        }

        repo.Delete(checklistItem);
        await _unitOfWork.SaveChangesAsync();
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
