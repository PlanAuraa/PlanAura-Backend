using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Planura.Core.Application.Abstraction.PaymentGateway;
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
    private const long PackageId = 99;
    private const decimal PackagePrice = 5000m;

    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Mock<IPaymentGatewayService> _paymentGatewayServiceMock = new();
    private readonly IMapper _mapper = new MapperConfiguration(
        cfg => cfg.AddProfile<MappingProfile>(),
        NullLoggerFactory.Instance).CreateMapper();

    private BookingService CreateService() => new(
        _unitOfWorkMock.Object,
        _mapper,
        _notificationServiceMock.Object,
        _paymentGatewayServiceMock.Object,
        Options.Create(new BookingOptions { HoldTtlHours = 48 }),
        Options.Create(new StripeOptions
        {
            SecretKey = "sk_test_x",
            PublishableKey = "pk_test_x",
            WebhookSecret = "whsec_x",
            DefaultCurrency = "EGP"
        }),
        NullLogger<BookingService>.Instance);

    private static CreateBookingRequestDto CreateValidDto(
        long? vendorPackageId = PackageId,
        int? guestCount = null,
        string paymentMethodId = "pm_card_visa",
        string requestId = "req-1") => new()
    {
        EventPlanId = EventPlanId,
        AvailabilityId = AvailabilityId,
        VendorPackageId = vendorPackageId,
        GuestCount = guestCount,
        PaymentMethodId = paymentMethodId,
        RequestId = requestId
    };

    private Mock<IGenericRepository<VendorPackage, long>> SetupPackageRepo(
        long vendorId = VendorId, decimal basePrice = PackagePrice, int? maxGuests = null)
    {
        var packageRepo = _unitOfWorkMock.SetupRepository<VendorPackage, long>();
        packageRepo.Setup(r => r.GetAsync(PackageId))
            .ReturnsAsync(new VendorPackage { Id = PackageId, VendorId = vendorId, Title = "Gold", BasePrice = basePrice, MaxGuests = maxGuests });
        return packageRepo;
    }

    private void SetupAuthorizeSucceeds(string paymentIntentId = "pi_auth_123")
    {
        _paymentGatewayServiceMock
            .Setup(g => g.AuthorizePaymentIntentAsync(It.IsAny<AuthorizePaymentIntentRequest>()))
            .ReturnsAsync(new PaymentIntentResult { PaymentIntentId = paymentIntentId, ClientSecret = "secret", Status = "requires_capture" });
    }

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

    private void SetupVendorUserRepo(Vendor? vendor)
    {
        var repo = _unitOfWorkMock.SetupRepository<Vendor, long>();
        repo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Vendor>>())).ReturnsAsync(vendor);
    }

    private static Payment CreateAuthorizedPayment(string gatewayReference = "pi_test") => new()
    {
        Id = 1,
        BookingRequestId = 1,
        ClientId = ClientId,
        VendorId = VendorId,
        Amount = PackagePrice,
        Status = PaymentStatus.Authorized,
        GatewayReference = gatewayReference
    };

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
    public async Task CreateBookingRequestAsync_GuestCountExceedsPackageMaxGuests_ThrowsBadRequestWithClearMessage()
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
        packageRepo.Setup(r => r.GetAsync(99))
            .ReturnsAsync(new VendorPackage { Id = 99, VendorId = VendorId, Title = "Gold", BasePrice = 5000m, MaxGuests = 100 });

        var service = CreateService();
        var dto = new CreateBookingRequestDto
        {
            EventPlanId = EventPlanId,
            AvailabilityId = AvailabilityId,
            VendorPackageId = 99,
            GuestCount = 101
        };

        var ex = await Assert.ThrowsAsync<BadRequestExeption>(() => service.CreateBookingRequestAsync(ClientUserId, dto));
        Assert.Equal("Guest count exceeds the package maximum of 100 guests.", ex.Message);
    }

    [Fact]
    public async Task CreateBookingRequestAsync_GuestCountAtPackageMaxGuests_Succeeds()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan());

        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>()))
            .ReturnsAsync(CreateSlot());

        var userRepo = _unitOfWorkMock.SetupRepository<ApplicationUser, long>();
        userRepo.Setup(r => r.GetAsync(VendorUserId)).ReturnsAsync(CreateVendorUser());

        SetupPackageRepo(maxGuests: 100);
        SetupAuthorizeSucceeds();
        _unitOfWorkMock.SetupRepository<Payment, long>();

        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        BookingRequest? capturedBooking = null;
        bookingRepo.Setup(r => r.AddAsync(It.IsAny<BookingRequest>()))
            .Callback<BookingRequest>(b => capturedBooking = b)
            .Returns(Task.CompletedTask);

        _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();

        var service = CreateService();
        var dto = CreateValidDto(guestCount: 100);

        await service.CreateBookingRequestAsync(ClientUserId, dto);

        Assert.NotNull(capturedBooking);
        Assert.Equal(100, capturedBooking!.GuestCount);
        Assert.Equal(PackagePrice, capturedBooking.AgreedPrice);
    }

    [Fact]
    public async Task CreateBookingRequestAsync_PackageHasNoMaxGuestsCap_AllowsAnyGuestCount()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan());

        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>()))
            .ReturnsAsync(CreateSlot());

        var userRepo = _unitOfWorkMock.SetupRepository<ApplicationUser, long>();
        userRepo.Setup(r => r.GetAsync(VendorUserId)).ReturnsAsync(CreateVendorUser());

        SetupPackageRepo(maxGuests: null);
        SetupAuthorizeSucceeds();
        _unitOfWorkMock.SetupRepository<Payment, long>();

        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        BookingRequest? capturedBooking = null;
        bookingRepo.Setup(r => r.AddAsync(It.IsAny<BookingRequest>()))
            .Callback<BookingRequest>(b => capturedBooking = b)
            .Returns(Task.CompletedTask);

        _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();

        var service = CreateService();
        var dto = CreateValidDto(guestCount: 100_000);

        await service.CreateBookingRequestAsync(ClientUserId, dto);

        Assert.NotNull(capturedBooking);
        Assert.Equal(100_000, capturedBooking!.GuestCount);
        Assert.Equal(PackagePrice, capturedBooking.AgreedPrice);
    }

    [Fact]
    public async Task CreateBookingRequestAsync_NoPackageSelected_ThrowsBadRequest()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan());

        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>()))
            .ReturnsAsync(CreateSlot());

        var userRepo = _unitOfWorkMock.SetupRepository<ApplicationUser, long>();
        userRepo.Setup(r => r.GetAsync(VendorUserId)).ReturnsAsync(CreateVendorUser());

        var service = CreateService();
        var dto = CreateValidDto(vendorPackageId: null);

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.CreateBookingRequestAsync(ClientUserId, dto));

        _paymentGatewayServiceMock.Verify(g => g.AuthorizePaymentIntentAsync(It.IsAny<AuthorizePaymentIntentRequest>()), Times.Never);
    }

    [Fact]
    public async Task CreateBookingRequestAsync_MissingPaymentMethodId_ThrowsBadRequest()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan());

        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>()))
            .ReturnsAsync(CreateSlot());

        var userRepo = _unitOfWorkMock.SetupRepository<ApplicationUser, long>();
        userRepo.Setup(r => r.GetAsync(VendorUserId)).ReturnsAsync(CreateVendorUser());

        SetupPackageRepo();

        var service = CreateService();
        var dto = CreateValidDto(paymentMethodId: "");

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.CreateBookingRequestAsync(ClientUserId, dto));

        _paymentGatewayServiceMock.Verify(g => g.AuthorizePaymentIntentAsync(It.IsAny<AuthorizePaymentIntentRequest>()), Times.Never);
    }

    [Fact]
    public async Task CreateBookingRequestAsync_MissingRequestId_ThrowsBadRequest()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan());

        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>()))
            .ReturnsAsync(CreateSlot());

        var userRepo = _unitOfWorkMock.SetupRepository<ApplicationUser, long>();
        userRepo.Setup(r => r.GetAsync(VendorUserId)).ReturnsAsync(CreateVendorUser());

        SetupPackageRepo();

        var service = CreateService();
        var dto = CreateValidDto(requestId: "");

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.CreateBookingRequestAsync(ClientUserId, dto));

        _paymentGatewayServiceMock.Verify(g => g.AuthorizePaymentIntentAsync(It.IsAny<AuthorizePaymentIntentRequest>()), Times.Never);
    }

    [Fact]
    public async Task CreateBookingRequestAsync_AuthorizationDeclined_PropagatesAndNeverCreatesBooking()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan());

        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>()))
            .ReturnsAsync(CreateSlot());

        var userRepo = _unitOfWorkMock.SetupRepository<ApplicationUser, long>();
        userRepo.Setup(r => r.GetAsync(VendorUserId)).ReturnsAsync(CreateVendorUser());

        SetupPackageRepo();

        _paymentGatewayServiceMock
            .Setup(g => g.AuthorizePaymentIntentAsync(It.IsAny<AuthorizePaymentIntentRequest>()))
            .ThrowsAsync(new PaymentDeclinedExeption("Your card was declined: insufficient_funds"));

        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();

        var service = CreateService();
        var dto = CreateValidDto();

        await Assert.ThrowsAsync<PaymentDeclinedExeption>(() => service.CreateBookingRequestAsync(ClientUserId, dto));

        bookingRepo.Verify(r => r.AddAsync(It.IsAny<BookingRequest>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateBookingRequestAsync_Valid_AuthorizesPaymentAndCreatesAuthorizedPaymentRecord()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan());

        var slot = CreateSlot();
        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>())).ReturnsAsync(slot);

        var userRepo = _unitOfWorkMock.SetupRepository<ApplicationUser, long>();
        userRepo.Setup(r => r.GetAsync(VendorUserId)).ReturnsAsync(CreateVendorUser());

        SetupPackageRepo();
        SetupAuthorizeSucceeds("pi_auth_999");

        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        BookingRequest? capturedBooking = null;
        bookingRepo.Setup(r => r.AddAsync(It.IsAny<BookingRequest>()))
            .Callback<BookingRequest>(b => capturedBooking = b)
            .Returns(Task.CompletedTask);

        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        Payment? capturedPayment = null;
        paymentRepo.Setup(r => r.AddAsync(It.IsAny<Payment>()))
            .Callback<Payment>(p => capturedPayment = p)
            .Returns(Task.CompletedTask);

        var historyRepo = _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();
        BookingStatusHistory? capturedHistory = null;
        historyRepo.Setup(r => r.AddAsync(It.IsAny<BookingStatusHistory>()))
            .Callback<BookingStatusHistory>(h => capturedHistory = h)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var dto = CreateValidDto(guestCount: 100, paymentMethodId: "pm_card_visa", requestId: "req-abc");
        dto.ClientMessage = "Please confirm";

        var result = await service.CreateBookingRequestAsync(ClientUserId, dto);

        Assert.NotNull(capturedBooking);
        Assert.Equal(BookingStatus.Pending, capturedBooking!.Status);
        Assert.Equal(BookingPaymentStatus.Unpaid, capturedBooking.PaymentStatus);
        Assert.Equal(ClientId, capturedBooking.ClientId);
        Assert.Equal(VendorId, capturedBooking.VendorId);
        Assert.Equal(new DateOnly(2026, 8, 1), capturedBooking.EventDate);

        Assert.NotNull(capturedPayment);
        Assert.Equal(PaymentStatus.Authorized, capturedPayment!.Status);
        Assert.Equal("pi_auth_999", capturedPayment.GatewayReference);
        Assert.Equal("pm_card_visa", capturedPayment.PaymentMethod);
        Assert.Equal(PackagePrice, capturedPayment.Amount);
        Assert.NotNull(capturedPayment.AuthorizedAt);
        Assert.Same(capturedBooking, capturedPayment.BookingRequest);

        Assert.Equal(AvailabilityStatus.Held, slot.Status);
        Assert.NotNull(slot.HoldExpiresAt);
        Assert.Same(capturedBooking, slot.BookingRequest);

        Assert.NotNull(capturedHistory);
        Assert.Null(capturedHistory!.PreviousStatus);
        Assert.Equal("Pending", capturedHistory.NewStatus);
        Assert.Equal(ClientUserId, capturedHistory.ChangedByUserId);

        _paymentGatewayServiceMock.Verify(g => g.AuthorizePaymentIntentAsync(It.Is<AuthorizePaymentIntentRequest>(
            r => r.AmountInSmallestUnit == 500000
                && r.Currency == "egp"
                && r.PaymentMethodId == "pm_card_visa"
                && r.IdempotencyKey == "req-abc")), Times.Once);

        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _paymentGatewayServiceMock.Verify(g => g.CancelPaymentIntentAsync(It.IsAny<CancelPaymentIntentRequest>()), Times.Never);
        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            VendorUserId, NotificationTypes.BookingRequestReceived, It.IsAny<string>(), It.IsAny<string?>()), Times.Once);

        Assert.Equal(ClientId, result.ClientId);
    }

    [Fact]
    public async Task CreateBookingRequestAsync_ConcurrencyConflictOnCommit_ThrowsSlotUnavailableRollsBackAndVoidsAuthorization()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan());

        var slot = CreateSlot();
        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>())).ReturnsAsync(slot);

        var userRepo = _unitOfWorkMock.SetupRepository<ApplicationUser, long>();
        userRepo.Setup(r => r.GetAsync(VendorUserId)).ReturnsAsync(CreateVendorUser());

        SetupPackageRepo();
        SetupAuthorizeSucceeds("pi_auth_conflict");

        _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        _unitOfWorkMock.SetupRepository<Payment, long>();
        _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();

        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        var service = CreateService();
        var dto = CreateValidDto();

        await Assert.ThrowsAsync<SlotUnavailableExeption>(() => service.CreateBookingRequestAsync(ClientUserId, dto));

        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _paymentGatewayServiceMock.Verify(g => g.CancelPaymentIntentAsync(It.Is<CancelPaymentIntentRequest>(
            r => r.PaymentIntentId == "pi_auth_conflict")), Times.Once);
    }

    [Fact]
    public async Task CreateBookingRequestAsync_UniqueIndexViolationOnCommit_ThrowsSlotUnavailableRollsBackAndVoidsAuthorization()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan());

        var slot = CreateSlot();
        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>())).ReturnsAsync(slot);

        var userRepo = _unitOfWorkMock.SetupRepository<ApplicationUser, long>();
        userRepo.Setup(r => r.GetAsync(VendorUserId)).ReturnsAsync(CreateVendorUser());

        SetupPackageRepo();
        SetupAuthorizeSucceeds("pi_auth_unique");

        _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        _unitOfWorkMock.SetupRepository<Payment, long>();
        _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();

        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("duplicate key", new SqlException(2627)));

        var service = CreateService();
        var dto = CreateValidDto();

        await Assert.ThrowsAsync<SlotUnavailableExeption>(() => service.CreateBookingRequestAsync(ClientUserId, dto));

        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _paymentGatewayServiceMock.Verify(g => g.CancelPaymentIntentAsync(It.Is<CancelPaymentIntentRequest>(
            r => r.PaymentIntentId == "pi_auth_unique")), Times.Once);
    }

    [Fact]
    public async Task CreateBookingRequestAsync_CommitFailsAndCompensatingCancelAlsoFails_StillRethrowsOriginalException()
    {
        SetupClientRepo(CreateClient());
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan());

        var slot = CreateSlot();
        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>())).ReturnsAsync(slot);

        var userRepo = _unitOfWorkMock.SetupRepository<ApplicationUser, long>();
        userRepo.Setup(r => r.GetAsync(VendorUserId)).ReturnsAsync(CreateVendorUser());

        SetupPackageRepo();
        SetupAuthorizeSucceeds("pi_auth_double_fail");

        _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        _unitOfWorkMock.SetupRepository<Payment, long>();
        _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();

        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db unavailable"));

        _paymentGatewayServiceMock
            .Setup(g => g.CancelPaymentIntentAsync(It.IsAny<CancelPaymentIntentRequest>()))
            .ThrowsAsync(new InvalidOperationException("stripe unreachable"));

        var service = CreateService();
        var dto = CreateValidDto();

        // The original DB failure must still propagate even though the compensating cancel itself failed.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateBookingRequestAsync(ClientUserId, dto));
        Assert.Equal("db unavailable", ex.Message);

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

        var payment = new Payment { Id = 5, BookingRequestId = 1, ClientId = ClientId, VendorId = VendorId, Amount = 5000m, Status = PaymentStatus.Authorized, GatewayReference = "pi_to_void" };
        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Payment>>())).ReturnsAsync(payment);

        var service = CreateService();
        var result = await service.CancelBookingRequestAsync(1, ClientUserId);

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.NotNull(booking.CancelledAt);
        slotRepo.Verify(r => r.DeleteRange(holds), Times.Once);
        Assert.NotNull(capturedHistory);
        Assert.Equal("Pending", capturedHistory!.PreviousStatus);
        Assert.Equal("Cancelled", capturedHistory.NewStatus);

        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _paymentGatewayServiceMock.Verify(g => g.CancelPaymentIntentAsync(It.Is<CancelPaymentIntentRequest>(
            r => r.PaymentIntentId == "pi_to_void")), Times.Once);
        Assert.Equal(PaymentStatus.Cancelled, payment.Status);
        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            VendorUserId, NotificationTypes.BookingCancelled, It.IsAny<string>(), It.IsAny<string?>()), Times.Once);

        Assert.Equal(BookingStatus.Cancelled, result.Status);
    }

    // ---------------- AcceptBookingRequestAsync ----------------

    [Fact]
    public async Task AcceptBookingRequestAsync_BookingNotFound_ThrowsNotFound()
    {
        SetupVendorUserRepo(CreateVendor());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync((BookingRequest?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.AcceptBookingRequestAsync(1, VendorUserId));
    }

    [Fact]
    public async Task AcceptBookingRequestAsync_OwnedByAnotherVendor_ThrowsNotFound()
    {
        SetupVendorUserRepo(new Vendor { Id = 999, UserId = VendorUserId, BusinessName = "Other" });
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(CreateBooking());

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.AcceptBookingRequestAsync(1, VendorUserId));
    }

    [Fact]
    public async Task AcceptBookingRequestAsync_NotPending_ThrowsBadRequest()
    {
        SetupVendorUserRepo(CreateVendor());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(CreateBooking(status: BookingStatus.Accepted));

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.AcceptBookingRequestAsync(1, VendorUserId));
    }

    [Fact]
    public async Task AcceptBookingRequestAsync_NoAuthorizedPayment_ThrowsBadRequest()
    {
        SetupVendorUserRepo(CreateVendor());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(CreateBooking());

        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Payment>>())).ReturnsAsync((Payment?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.AcceptBookingRequestAsync(1, VendorUserId));
    }

    [Fact]
    public async Task AcceptBookingRequestAsync_Valid_CapturesPaymentAcceptsBookingAndBooksSlot()
    {
        SetupVendorUserRepo(CreateVendor());
        var booking = CreateBooking();
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(booking);

        var payment = CreateAuthorizedPayment("pi_accept_test");
        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Payment>>())).ReturnsAsync(payment);

        _paymentGatewayServiceMock
            .Setup(g => g.CapturePaymentIntentAsync(It.IsAny<CapturePaymentIntentRequest>()))
            .ReturnsAsync(new PaymentIntentResult { PaymentIntentId = "pi_accept_test", ClientSecret = "secret", Status = "succeeded" });

        var hold = CreateSlot(status: AvailabilityStatus.Held);
        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<VendorAvailability> { hold });

        var historyRepo = _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();
        BookingStatusHistory? capturedHistory = null;
        historyRepo.Setup(r => r.AddAsync(It.IsAny<BookingStatusHistory>()))
            .Callback<BookingStatusHistory>(h => capturedHistory = h)
            .Returns(Task.CompletedTask);

        var clientRepo = _unitOfWorkMock.SetupRepository<Client, long>();
        clientRepo.Setup(r => r.GetAsync(ClientId)).ReturnsAsync(CreateClient());

        var service = CreateService();
        var result = await service.AcceptBookingRequestAsync(1, VendorUserId);

        Assert.Equal(BookingStatus.Accepted, booking.Status);
        Assert.Equal(BookingPaymentStatus.Paid, booking.PaymentStatus);
        Assert.NotNull(booking.RespondedAt);

        Assert.Equal(PaymentStatus.Completed, payment.Status);
        Assert.NotNull(payment.PaidAt);

        Assert.Equal(AvailabilityStatus.Booked, hold.Status);
        Assert.Null(hold.HoldExpiresAt);

        Assert.NotNull(capturedHistory);
        Assert.Equal("Pending", capturedHistory!.PreviousStatus);
        Assert.Equal("Accepted", capturedHistory.NewStatus);
        Assert.Equal(VendorUserId, capturedHistory.ChangedByUserId);

        _paymentGatewayServiceMock.Verify(g => g.CapturePaymentIntentAsync(It.Is<CapturePaymentIntentRequest>(
            r => r.PaymentIntentId == "pi_accept_test")), Times.Once);

        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            ClientUserId, NotificationTypes.BookingAccepted, It.IsAny<string>(), It.IsAny<string?>()), Times.Once);

        Assert.Equal(BookingStatus.Accepted, result.Status);
    }

    [Fact]
    public async Task AcceptBookingRequestAsync_CaptureFails_AutoDeclinesBookingAndThrowsPaymentDeclined()
    {
        SetupVendorUserRepo(CreateVendor());
        var booking = CreateBooking();
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(booking);

        var payment = CreateAuthorizedPayment("pi_capture_fail");
        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Payment>>())).ReturnsAsync(payment);

        _paymentGatewayServiceMock
            .Setup(g => g.CapturePaymentIntentAsync(It.IsAny<CapturePaymentIntentRequest>()))
            .ThrowsAsync(new InvalidOperationException("card no longer valid"));

        var holdRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        holdRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<VendorAvailability>());

        _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();

        var clientRepo = _unitOfWorkMock.SetupRepository<Client, long>();
        clientRepo.Setup(r => r.GetAsync(ClientId)).ReturnsAsync(CreateClient());

        var service = CreateService();

        await Assert.ThrowsAsync<PaymentDeclinedExeption>(() => service.AcceptBookingRequestAsync(1, VendorUserId));

        Assert.Equal(BookingStatus.Rejected, booking.Status);
        Assert.Equal(PaymentStatus.Failed, payment.Status);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            ClientUserId, NotificationTypes.BookingRejected, It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
    }

    // ---------------- RejectBookingRequestAsync ----------------

    [Fact]
    public async Task RejectBookingRequestAsync_BookingNotFound_ThrowsNotFound()
    {
        SetupVendorUserRepo(CreateVendor());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync((BookingRequest?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.RejectBookingRequestAsync(1, VendorUserId, "no longer available"));
    }

    [Fact]
    public async Task RejectBookingRequestAsync_OwnedByAnotherVendor_ThrowsNotFound()
    {
        SetupVendorUserRepo(new Vendor { Id = 999, UserId = VendorUserId, BusinessName = "Other" });
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(CreateBooking());

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.RejectBookingRequestAsync(1, VendorUserId, null));
    }

    [Fact]
    public async Task RejectBookingRequestAsync_NotPending_ThrowsBadRequest()
    {
        SetupVendorUserRepo(CreateVendor());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(CreateBooking(status: BookingStatus.Rejected));

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.RejectBookingRequestAsync(1, VendorUserId, null));
    }

    [Fact]
    public async Task RejectBookingRequestAsync_Valid_RejectsDeletesHoldVoidsAuthorizationAndNotifiesClient()
    {
        SetupVendorUserRepo(CreateVendor());
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

        var payment = CreateAuthorizedPayment("pi_reject_test");
        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Payment>>())).ReturnsAsync(payment);

        var clientRepo = _unitOfWorkMock.SetupRepository<Client, long>();
        clientRepo.Setup(r => r.GetAsync(ClientId)).ReturnsAsync(CreateClient());

        var service = CreateService();
        var result = await service.RejectBookingRequestAsync(1, VendorUserId, "Fully booked that day");

        Assert.Equal(BookingStatus.Rejected, booking.Status);
        Assert.Equal("Fully booked that day", booking.VendorResponse);
        Assert.NotNull(booking.RespondedAt);
        slotRepo.Verify(r => r.DeleteRange(holds), Times.Once);

        Assert.NotNull(capturedHistory);
        Assert.Equal("Pending", capturedHistory!.PreviousStatus);
        Assert.Equal("Rejected", capturedHistory.NewStatus);
        Assert.Equal(VendorUserId, capturedHistory.ChangedByUserId);

        _paymentGatewayServiceMock.Verify(g => g.CancelPaymentIntentAsync(It.Is<CancelPaymentIntentRequest>(
            r => r.PaymentIntentId == "pi_reject_test")), Times.Once);
        Assert.Equal(PaymentStatus.Cancelled, payment.Status);

        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            ClientUserId, NotificationTypes.BookingRejected, It.IsAny<string>(), It.IsAny<string?>()), Times.Once);

        Assert.Equal(BookingStatus.Rejected, result.Status);
    }

    [Fact]
    public async Task RejectBookingRequestAsync_VoidFails_StillCompletesRejection()
    {
        SetupVendorUserRepo(CreateVendor());
        var booking = CreateBooking();
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(booking);

        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<VendorAvailability>());

        _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();

        var payment = CreateAuthorizedPayment("pi_reject_void_fail");
        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Payment>>())).ReturnsAsync(payment);

        _paymentGatewayServiceMock
            .Setup(g => g.CancelPaymentIntentAsync(It.IsAny<CancelPaymentIntentRequest>()))
            .ThrowsAsync(new InvalidOperationException("stripe unreachable"));

        var clientRepo = _unitOfWorkMock.SetupRepository<Client, long>();
        clientRepo.Setup(r => r.GetAsync(ClientId)).ReturnsAsync(CreateClient());

        var service = CreateService();
        var result = await service.RejectBookingRequestAsync(1, VendorUserId, null);

        Assert.Equal(BookingStatus.Rejected, booking.Status);
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
        Assert.Equal(BookingStatus.Rejected, result.Status);
    }

    [Fact]
    public async Task RejectBookingRequestAsync_NoAuthorizedPayment_StillRejectsWithoutVoiding()
    {
        SetupVendorUserRepo(CreateVendor());
        var booking = CreateBooking();
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(booking);

        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<VendorAvailability>());

        _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();

        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Payment>>())).ReturnsAsync((Payment?)null);

        var clientRepo = _unitOfWorkMock.SetupRepository<Client, long>();
        clientRepo.Setup(r => r.GetAsync(ClientId)).ReturnsAsync(CreateClient());

        var service = CreateService();
        await service.RejectBookingRequestAsync(1, VendorUserId, null);

        Assert.Equal(BookingStatus.Rejected, booking.Status);
        _paymentGatewayServiceMock.Verify(g => g.CancelPaymentIntentAsync(It.IsAny<CancelPaymentIntentRequest>()), Times.Never);
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
