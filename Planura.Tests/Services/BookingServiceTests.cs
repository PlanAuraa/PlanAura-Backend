using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Planura.Core.Application.Common;
using Planura.Core.Application.Mappings;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;
using Planura.Tests.TestHelpers;
using Xunit;

namespace Planura.Tests.Services;

public class BookingServiceTests
{
    private const long ClientUserId = 500;
    private const long ClientId = 10;
    private const long VendorId = 20;
    private const long VendorUserId = 200;
    private const long EventPlanId = 30;
    private const long AvailabilityId = 40;

    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly IMapper _mapper = new MapperConfiguration(
        cfg => cfg.AddProfile<MappingProfile>(),
        NullLoggerFactory.Instance).CreateMapper();

    private BookingService CreateService() => new(
        _unitOfWorkMock.Object,
        _mapper,
        _notificationServiceMock.Object,
        Options.Create(new BookingOptions { HoldTtlHours = 48, PaymentDeadlineHours = 72 }));

    private static Client CreateClient() => new() { Id = ClientId, UserId = ClientUserId };

    private static EventPlan CreateEventPlan(long clientId = ClientId) => new()
    {
        Id = EventPlanId,
        ClientId = clientId,
        EventType = "Wedding"
    };

    private static Vendor CreateVendor(string verificationStatus = "verified") => new()
    {
        Id = VendorId,
        UserId = VendorUserId,
        BusinessName = "Test Vendor",
        VerificationStatus = verificationStatus
    };

    private static ApplicationUser CreateVendorUser(bool isActive = true) => new()
    {
        Id = VendorUserId,
        FullName = "Vendor User",
        IsActive = isActive
    };

    private static VendorAvailability CreateSlot(AvailabilityStatus status = AvailabilityStatus.Available, Vendor? vendor = null) => new()
    {
        Id = AvailabilityId,
        VendorId = VendorId,
        StartAt = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero),
        EndAt = new DateTimeOffset(2026, 8, 1, 14, 0, 0, TimeSpan.Zero),
        Status = status,
        Vendor = vendor ?? CreateVendor()
    };

    private static BookingRequest CreateBooking(BookingStatus status = BookingStatus.Pending, long clientId = ClientId) => new()
    {
        Id = 1,
        EventPlanId = EventPlanId,
        ClientId = clientId,
        VendorId = VendorId,
        EventDate = new DateOnly(2026, 8, 1),
        Status = status,
        PaymentStatus = BookingPaymentStatus.Unpaid
    };

    private void SetupClientRepo(Client? client)
    {
        var repo = _unitOfWorkMock.SetupRepository<Client, long>();
        repo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Client>>())).ReturnsAsync(client);
    }

    // ---------------- CreateBookingRequestAsync ----------------

    [Fact]
    public async Task CreateBookingRequestAsync_ClientNotFound_ThrowsNotFound()
    {
        SetupClientRepo(null);

        var service = CreateService();
        var dto = new CreateBookingRequestDto { EventPlanId = EventPlanId, AvailabilityId = AvailabilityId };

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.CreateBookingRequestAsync(ClientUserId, dto));
    }

    [Fact]
    public async Task CreateBookingRequestAsync_EventPlanNotFound_ThrowsNotFound()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync((EventPlan?)null);

        var service = CreateService();
        var dto = new CreateBookingRequestDto { EventPlanId = EventPlanId, AvailabilityId = AvailabilityId };

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.CreateBookingRequestAsync(ClientUserId, dto));
    }

    [Fact]
    public async Task CreateBookingRequestAsync_EventPlanOwnedByAnotherClient_ThrowsNotFound()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan(clientId: 999));

        var service = CreateService();
        var dto = new CreateBookingRequestDto { EventPlanId = EventPlanId, AvailabilityId = AvailabilityId };

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.CreateBookingRequestAsync(ClientUserId, dto));
    }

    [Fact]
    public async Task CreateBookingRequestAsync_SlotNotFound_ThrowsNotFound()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan());

        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>()))
            .ReturnsAsync((VendorAvailability?)null);

        var service = CreateService();
        var dto = new CreateBookingRequestDto { EventPlanId = EventPlanId, AvailabilityId = AvailabilityId };

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.CreateBookingRequestAsync(ClientUserId, dto));
    }

    [Fact]
    public async Task CreateBookingRequestAsync_SlotNotAvailable_ThrowsSlotUnavailable()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan());

        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>()))
            .ReturnsAsync(CreateSlot(status: AvailabilityStatus.Held));

        var service = CreateService();
        var dto = new CreateBookingRequestDto { EventPlanId = EventPlanId, AvailabilityId = AvailabilityId };

        await Assert.ThrowsAsync<SlotUnavailableExeption>(() => service.CreateBookingRequestAsync(ClientUserId, dto));
    }

    [Fact]
    public async Task CreateBookingRequestAsync_VendorNotVerified_ThrowsBadRequest()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan());

        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>()))
            .ReturnsAsync(CreateSlot(vendor: CreateVendor(verificationStatus: "pending")));

        var service = CreateService();
        var dto = new CreateBookingRequestDto { EventPlanId = EventPlanId, AvailabilityId = AvailabilityId };

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.CreateBookingRequestAsync(ClientUserId, dto));
    }

    [Fact]
    public async Task CreateBookingRequestAsync_VendorUserInactive_ThrowsBadRequest()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan());

        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>()))
            .ReturnsAsync(CreateSlot());

        var userRepo = _unitOfWorkMock.SetupRepository<ApplicationUser, long>();
        userRepo.Setup(r => r.GetAsync(VendorUserId)).ReturnsAsync(CreateVendorUser(isActive: false));

        var service = CreateService();
        var dto = new CreateBookingRequestDto { EventPlanId = EventPlanId, AvailabilityId = AvailabilityId };

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.CreateBookingRequestAsync(ClientUserId, dto));
    }

    [Fact]
    public async Task CreateBookingRequestAsync_VendorPackageBelongsToAnotherVendor_ThrowsNotFound()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan());

        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>()))
            .ReturnsAsync(CreateSlot());

        var userRepo = _unitOfWorkMock.SetupRepository<ApplicationUser, long>();
        userRepo.Setup(r => r.GetAsync(VendorUserId)).ReturnsAsync(CreateVendorUser());

        var packageRepo = _unitOfWorkMock.SetupRepository<VendorPackage, long>();
        packageRepo.Setup(r => r.GetAsync(99)).ReturnsAsync(new VendorPackage { Id = 99, VendorId = 999, Title = "Other" });

        var service = CreateService();
        var dto = new CreateBookingRequestDto { EventPlanId = EventPlanId, AvailabilityId = AvailabilityId, VendorPackageId = 99 };

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.CreateBookingRequestAsync(ClientUserId, dto));
    }

    [Fact]
    public async Task CreateBookingRequestAsync_Valid_CreatesBookingHoldsSlotAndWritesHistory()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan());

        var slot = CreateSlot();
        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>())).ReturnsAsync(slot);

        var userRepo = _unitOfWorkMock.SetupRepository<ApplicationUser, long>();
        userRepo.Setup(r => r.GetAsync(VendorUserId)).ReturnsAsync(CreateVendorUser());

        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        BookingRequest? capturedBooking = null;
        bookingRepo.Setup(r => r.AddAsync(It.IsAny<BookingRequest>()))
            .Callback<BookingRequest>(b => capturedBooking = b)
            .Returns(Task.CompletedTask);

        var historyRepo = _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();
        BookingStatusHistory? capturedHistory = null;
        historyRepo.Setup(r => r.AddAsync(It.IsAny<BookingStatusHistory>()))
            .Callback<BookingStatusHistory>(h => capturedHistory = h)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var dto = new CreateBookingRequestDto
        {
            EventPlanId = EventPlanId,
            AvailabilityId = AvailabilityId,
            GuestCount = 100,
            ClientMessage = "Please confirm"
        };

        var result = await service.CreateBookingRequestAsync(ClientUserId, dto);

        Assert.NotNull(capturedBooking);
        Assert.Equal(BookingStatus.Pending, capturedBooking!.Status);
        Assert.Equal(BookingPaymentStatus.Unpaid, capturedBooking.PaymentStatus);
        Assert.Equal(ClientId, capturedBooking.ClientId);
        Assert.Equal(VendorId, capturedBooking.VendorId);
        Assert.Equal(new DateOnly(2026, 8, 1), capturedBooking.EventDate);

        Assert.Equal(AvailabilityStatus.Held, slot.Status);
        Assert.NotNull(slot.HoldExpiresAt);
        Assert.Same(capturedBooking, slot.BookingRequest);

        Assert.NotNull(capturedHistory);
        Assert.Null(capturedHistory!.PreviousStatus);
        Assert.Equal("Pending", capturedHistory.NewStatus);
        Assert.Equal(ClientUserId, capturedHistory.ChangedByUserId);

        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            VendorUserId, NotificationTypes.BookingRequestReceived, It.IsAny<string>(), It.IsAny<string?>()), Times.Once);

        Assert.Equal(ClientId, result.ClientId);
    }

    [Fact]
    public async Task CreateBookingRequestAsync_ConcurrencyConflictOnCommit_ThrowsSlotUnavailableAndRollsBack()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan());

        var slot = CreateSlot();
        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>())).ReturnsAsync(slot);

        var userRepo = _unitOfWorkMock.SetupRepository<ApplicationUser, long>();
        userRepo.Setup(r => r.GetAsync(VendorUserId)).ReturnsAsync(CreateVendorUser());

        _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();

        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        var service = CreateService();
        var dto = new CreateBookingRequestDto { EventPlanId = EventPlanId, AvailabilityId = AvailabilityId };

        await Assert.ThrowsAsync<SlotUnavailableExeption>(() => service.CreateBookingRequestAsync(ClientUserId, dto));

        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateBookingRequestAsync_UniqueIndexViolationOnCommit_ThrowsSlotUnavailableAndRollsBack()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan());

        var slot = CreateSlot();
        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>())).ReturnsAsync(slot);

        var userRepo = _unitOfWorkMock.SetupRepository<ApplicationUser, long>();
        userRepo.Setup(r => r.GetAsync(VendorUserId)).ReturnsAsync(CreateVendorUser());

        _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();

        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("duplicate key", new SqlException(2627)));

        var service = CreateService();
        var dto = new CreateBookingRequestDto { EventPlanId = EventPlanId, AvailabilityId = AvailabilityId };

        await Assert.ThrowsAsync<SlotUnavailableExeption>(() => service.CreateBookingRequestAsync(ClientUserId, dto));

        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------- CancelBookingRequestAsync ----------------

    [Fact]
    public async Task CancelBookingRequestAsync_BookingNotFound_ThrowsNotFound()
    {
        SetupClientRepo(CreateClient());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync((BookingRequest?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.CancelBookingRequestAsync(1, ClientUserId));
    }

    [Fact]
    public async Task CancelBookingRequestAsync_OwnedByAnotherClient_ThrowsNotFound()
    {
        SetupClientRepo(CreateClient());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(CreateBooking(clientId: 999));

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.CancelBookingRequestAsync(1, ClientUserId));
    }

    [Fact]
    public async Task CancelBookingRequestAsync_NotPending_ThrowsBadRequest()
    {
        SetupClientRepo(CreateClient());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(CreateBooking(status: BookingStatus.Accepted));

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.CancelBookingRequestAsync(1, ClientUserId));
    }

    [Fact]
    public async Task CancelBookingRequestAsync_Valid_CancelsBookingDeletesHoldAndWritesHistory()
    {
        SetupClientRepo(CreateClient());
        var booking = CreateBooking();
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(booking);

        var holds = new List<VendorAvailability> { CreateSlot(status: AvailabilityStatus.Held) };
        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>(), It.IsAny<bool>()))
            .ReturnsAsync(holds);

        var historyRepo = _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();
        BookingStatusHistory? capturedHistory = null;
        historyRepo.Setup(r => r.AddAsync(It.IsAny<BookingStatusHistory>()))
            .Callback<BookingStatusHistory>(h => capturedHistory = h)
            .Returns(Task.CompletedTask);

        var vendorRepo = _unitOfWorkMock.SetupRepository<Vendor, long>();
        vendorRepo.Setup(r => r.GetAsync(VendorId)).ReturnsAsync(CreateVendor());

        var service = CreateService();
        var result = await service.CancelBookingRequestAsync(1, ClientUserId);

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.NotNull(booking.CancelledAt);
        slotRepo.Verify(r => r.DeleteRange(holds), Times.Once);
        Assert.NotNull(capturedHistory);
        Assert.Equal("Pending", capturedHistory!.PreviousStatus);
        Assert.Equal("Cancelled", capturedHistory.NewStatus);

        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            VendorUserId, NotificationTypes.BookingCancelled, It.IsAny<string>(), It.IsAny<string?>()), Times.Once);

        Assert.Equal(BookingStatus.Cancelled, result.Status);
    }

    // ---------------- GetBookingRequestAsync ----------------

    [Fact]
    public async Task GetBookingRequestAsync_NotFound_ThrowsNotFound()
    {
        SetupClientRepo(CreateClient());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync((BookingRequest?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.GetBookingRequestAsync(1, ClientUserId));
    }

    [Fact]
    public async Task GetBookingRequestAsync_OwnedByAnotherClient_ThrowsNotFound()
    {
        SetupClientRepo(CreateClient());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(CreateBooking(clientId: 999));

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.GetBookingRequestAsync(1, ClientUserId));
    }

    [Fact]
    public async Task GetBookingRequestAsync_Valid_ReturnsDto()
    {
        SetupClientRepo(CreateClient());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(CreateBooking());

        var service = CreateService();
        var result = await service.GetBookingRequestAsync(1, ClientUserId);

        Assert.Equal(1, result.Id);
        Assert.Equal(ClientId, result.ClientId);
    }

    // ---------------- ListMyBookingRequestsAsync ----------------

    [Fact]
    public async Task ListMyBookingRequestsAsync_Valid_ReturnsPagedResult()
    {
        SetupClientRepo(CreateClient());
        var bookings = new List<BookingRequest> { CreateBooking() };

        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetCountAsync(It.IsAny<ISpecification<BookingRequest>>())).ReturnsAsync(1);
        bookingRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<BookingRequest>>(), It.IsAny<bool>()))
            .ReturnsAsync(bookings);

        var service = CreateService();
        var result = await service.ListMyBookingRequestsAsync(ClientUserId, new BookingRequestFilterDto());

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
    }

    // ---------------- FlagDisputeAsync ----------------

    [Fact]
    public async Task FlagDisputeAsync_EmptyReason_ThrowsBadRequest()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.FlagDisputeAsync(1, ClientUserId, ""));
    }

    [Fact]
    public async Task FlagDisputeAsync_BookingNotFound_ThrowsNotFound()
    {
        SetupClientRepo(CreateClient());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync((BookingRequest?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.FlagDisputeAsync(1, ClientUserId, "Vendor never showed up."));
    }

    [Fact]
    public async Task FlagDisputeAsync_WrongStatus_ThrowsBadRequest()
    {
        SetupClientRepo(CreateClient());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(CreateBooking(status: BookingStatus.Pending));

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.FlagDisputeAsync(1, ClientUserId, "Vendor never showed up."));
    }

    [Fact]
    public async Task FlagDisputeAsync_AlreadyDisputed_ThrowsBadRequest()
    {
        SetupClientRepo(CreateClient());
        var booking = CreateBooking(status: BookingStatus.Accepted);
        booking.DisputeStatus = DisputeStatus.Open;

        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(booking);

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.FlagDisputeAsync(1, ClientUserId, "Vendor never showed up."));
    }

    [Fact]
    public async Task FlagDisputeAsync_Valid_SetsDisputeOpenAndWritesHistory()
    {
        SetupClientRepo(CreateClient());
        var booking = CreateBooking(status: BookingStatus.Accepted);

        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(booking);

        var historyRepo = _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();
        BookingStatusHistory? capturedHistory = null;
        historyRepo.Setup(r => r.AddAsync(It.IsAny<BookingStatusHistory>()))
            .Callback<BookingStatusHistory>(h => capturedHistory = h)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var result = await service.FlagDisputeAsync(1, ClientUserId, "Vendor never showed up.");

        Assert.Equal(DisputeStatus.Open, booking.DisputeStatus);
        Assert.NotNull(booking.DisputedAt);
        Assert.Equal(ClientUserId, booking.DisputedByUserId);
        Assert.NotNull(capturedHistory);
        Assert.Contains("Vendor never showed up.", capturedHistory!.Notes);
        Assert.Equal(DisputeStatus.Open, result.DisputeStatus);
    }
}

internal class DbUpdateConcurrencyException : Exception
{
}

internal class DbUpdateException : Exception
{
    public DbUpdateException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

internal class SqlException : Exception
{
    public int Number { get; }

    public SqlException(int number)
    {
        Number = number;
    }
}
