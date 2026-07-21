using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Planura.Core.Application.Mappings;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;
using Planura.Tests.TestHelpers;
using Xunit;

namespace Planura.Tests.Services;

public class EventPlanServiceTests
{
    private const long ClientUserId = 500;
    private const long ClientId = 10;
    private const long EventPlanId = 30;

    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly IMapper _mapper = new MapperConfiguration(
        cfg => cfg.AddProfile<MappingProfile>(),
        NullLoggerFactory.Instance).CreateMapper();

    private EventPlanService CreateService() => new(_unitOfWorkMock.Object, _mapper);

    private static Client CreateClient() => new() { Id = ClientId, UserId = ClientUserId };

    private static EventPlan CreateEventPlan(long clientId = ClientId) => new()
    {
        Id = EventPlanId,
        ClientId = clientId,
        EventType = "Wedding"
    };

    private void SetupClientRepo(Client? client)
    {
        var repo = _unitOfWorkMock.SetupRepository<Client, long>();
        repo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Client>>())).ReturnsAsync(client);
    }

    // ---------------- CreateEventPlanAsync ----------------

    [Fact]
    public async Task CreateEventPlanAsync_MissingEventType_ThrowsBadRequest()
    {
        var service = CreateService();
        var dto = new CreateEventPlanDto { EventType = "" };

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.CreateEventPlanAsync(ClientUserId, dto));
    }

    [Fact]
    public async Task CreateEventPlanAsync_ClientNotFound_ThrowsNotFound()
    {
        SetupClientRepo(null);
        var service = CreateService();
        var dto = new CreateEventPlanDto { EventType = "Wedding" };

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.CreateEventPlanAsync(ClientUserId, dto));
    }

    [Fact]
    public async Task CreateEventPlanAsync_Valid_CreatesEventPlan()
    {
        SetupClientRepo(CreateClient());

        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        EventPlan? captured = null;
        eventPlanRepo.Setup(r => r.AddAsync(It.IsAny<EventPlan>()))
            .Callback<EventPlan>(p => captured = p)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var dto = new CreateEventPlanDto
        {
            Title = "My Wedding",
            EventType = "Wedding",
            City = "Cairo",
            GuestCount = 150
        };

        var result = await service.CreateEventPlanAsync(ClientUserId, dto);

        Assert.NotNull(captured);
        Assert.Equal(ClientId, captured!.ClientId);
        Assert.Equal("My Wedding", captured.Title);
        Assert.Equal("Wedding", captured.EventType);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(ClientId, result.ClientId);
    }

    // ---------------- ListMyEventPlansAsync ----------------

    [Fact]
    public async Task ListMyEventPlansAsync_Valid_ReturnsOwnPlans()
    {
        SetupClientRepo(CreateClient());

        var plans = new List<EventPlan> { CreateEventPlan() };
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<EventPlan>>(), It.IsAny<bool>()))
            .ReturnsAsync(plans);

        var service = CreateService();
        var result = await service.ListMyEventPlansAsync(ClientUserId);

        Assert.Single(result);
    }

    // ---------------- GetEventPlanAsync ----------------

    [Fact]
    public async Task GetEventPlanAsync_NotFound_ThrowsNotFound()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync((EventPlan?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.GetEventPlanAsync(EventPlanId, ClientUserId));
    }

    [Fact]
    public async Task GetEventPlanAsync_OwnedByAnotherClient_ThrowsNotFound()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan(clientId: 999));

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.GetEventPlanAsync(EventPlanId, ClientUserId));
    }

    [Fact]
    public async Task GetEventPlanAsync_Valid_ReturnsDto()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan());

        var service = CreateService();
        var result = await service.GetEventPlanAsync(EventPlanId, ClientUserId);

        Assert.Equal(EventPlanId, result.Id);
        Assert.Equal(ClientId, result.ClientId);
    }

    // ---------------- UpdateEventPlanAsync ----------------

    [Fact]
    public async Task UpdateEventPlanAsync_MissingEventType_ThrowsBadRequest()
    {
        var service = CreateService();
        var dto = new UpdateEventPlanDto { EventType = "" };

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.UpdateEventPlanAsync(EventPlanId, ClientUserId, dto));
    }

    [Fact]
    public async Task UpdateEventPlanAsync_NotFound_ThrowsNotFound()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync((EventPlan?)null);

        var service = CreateService();
        var dto = new UpdateEventPlanDto { EventType = "Wedding" };

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.UpdateEventPlanAsync(EventPlanId, ClientUserId, dto));
    }

    [Fact]
    public async Task UpdateEventPlanAsync_OwnedByAnotherClient_ThrowsNotFound()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan(clientId: 999));

        var service = CreateService();
        var dto = new UpdateEventPlanDto { EventType = "Wedding" };

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.UpdateEventPlanAsync(EventPlanId, ClientUserId, dto));
    }

    [Fact]
    public async Task UpdateEventPlanAsync_Valid_UpdatesFieldsAndReturnsDto()
    {
        SetupClientRepo(CreateClient());
        var eventPlan = CreateEventPlan();
        eventPlan.Title = "Old Title";
        eventPlan.City = "Alexandria";
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(eventPlan);

        var service = CreateService();
        var dto = new UpdateEventPlanDto
        {
            Title = "New Title",
            EventType = "Birthday",
            EventDate = new DateOnly(2026, 9, 1),
            City = "Cairo",
            GuestCount = 80,
            BudgetTotal = 25000m,
            StyleNotes = "Rustic theme"
        };

        var result = await service.UpdateEventPlanAsync(EventPlanId, ClientUserId, dto);

        Assert.Equal("New Title", eventPlan.Title);
        Assert.Equal("Birthday", eventPlan.EventType);
        Assert.Equal(new DateOnly(2026, 9, 1), eventPlan.EventDate);
        Assert.Equal("Cairo", eventPlan.City);
        Assert.Equal(80, eventPlan.GuestCount);
        Assert.Equal(25000m, eventPlan.BudgetTotal);
        Assert.Equal("Rustic theme", eventPlan.StyleNotes);

        eventPlanRepo.Verify(r => r.Update(eventPlan), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        Assert.Equal(EventPlanId, result.Id);
        Assert.Equal("New Title", result.Title);
        Assert.Equal("Birthday", result.EventType);
    }

    // ---------------- DeleteEventPlanAsync ----------------

    [Fact]
    public async Task DeleteEventPlanAsync_NotFound_ThrowsNotFound()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync((EventPlan?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.DeleteEventPlanAsync(EventPlanId, ClientUserId));
    }

    [Fact]
    public async Task DeleteEventPlanAsync_OwnedByAnotherClient_ThrowsNotFound()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan(clientId: 999));

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.DeleteEventPlanAsync(EventPlanId, ClientUserId));
    }

    [Fact]
    public async Task DeleteEventPlanAsync_HasLinkedBookings_ThrowsBadRequest()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan());

        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetCountAsync(It.IsAny<ISpecification<BookingRequest>>())).ReturnsAsync(1);

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.DeleteEventPlanAsync(EventPlanId, ClientUserId));

        eventPlanRepo.Verify(r => r.Delete(It.IsAny<EventPlan>()), Times.Never);
    }

    [Fact]
    public async Task DeleteEventPlanAsync_NoLinkedBookings_DeletesEventPlan()
    {
        SetupClientRepo(CreateClient());
        var eventPlan = CreateEventPlan();
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(eventPlan);

        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetCountAsync(It.IsAny<ISpecification<BookingRequest>>())).ReturnsAsync(0);

        var service = CreateService();
        await service.DeleteEventPlanAsync(EventPlanId, ClientUserId);

        eventPlanRepo.Verify(r => r.Delete(eventPlan), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
