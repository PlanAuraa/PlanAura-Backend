using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Planura.Core.Application.Abstraction.AttachementService;
using Planura.Core.Application.Abstraction.BookingAgreement;
using Planura.Core.Application.Abstraction.PaymentGateway;
using Planura.Core.Application.Common;
using Planura.Core.Application.Services.Contract;
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

    private const string AgreementToken = "agreement-token";

    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Mock<IPaymentGatewayService> _paymentGatewayServiceMock = new();
    private readonly Mock<IContractService> _contractServiceMock = new();
    private readonly Mock<IAttachmentService> _attachmentServiceMock = new();
    private readonly Mock<IAgreementPreviewStore> _agreementPreviewStoreMock = new();
    private readonly IMapper _mapper = new MapperConfiguration(
        cfg => cfg.AddProfile<MappingProfile>(),
        NullLoggerFactory.Instance).CreateMapper();

    private BookingService CreateService() => new(
        _unitOfWorkMock.Object,
        _mapper,
        _notificationServiceMock.Object,
        _paymentGatewayServiceMock.Object,
        _contractServiceMock.Object,
        _attachmentServiceMock.Object,
        _agreementPreviewStoreMock.Object,
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
        RequestId = requestId,
        AgreementToken = AgreementToken,
        AgreementAccepted = true
    };

    /// <summary>Makes the reviewed Booking Agreement token resolve to an entry for this client, as it
    /// would after a preview call — required by the create happy paths.</summary>
    private void SetupAgreementToken(long clientId = ClientId, string token = AgreementToken)
    {
        _agreementPreviewStoreMock.Setup(s => s.TryGet(token))
            .Returns(new AgreementPreviewEntry(clientId, "PLN-CN-TEST", "images/booking-contracts/test.pdf", DateTimeOffset.UtcNow));
    }

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

        // Every create path that reaches authorization has also passed the reviewed-agreement gate, so
        // give those happy paths a valid agreement token to redeem.
        SetupAgreementToken();
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

    private static VendorAvailability CreateSlot(
        AvailabilityStatus status = AvailabilityStatus.Available, Vendor? vendor = null, DateTimeOffset? startAt = null)
    {
        // Default is a fixed date in the past relative to any realistic test run, so it always lands on
        // the full-payment path (event within the threshold) — deposit tests pass an explicit future startAt.
        var start = startAt ?? new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);
        return new()
        {
            Id = AvailabilityId,
            VendorId = VendorId,
            StartAt = start,
            EndAt = start.AddHours(4),
            Status = status,
            Vendor = vendor ?? CreateVendor()
        };
    }

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

    private Mock<IGenericRepository<Vendor, long>> SetupVendorUserRepo(Vendor? vendor)
    {
        var repo = _unitOfWorkMock.SetupRepository<Vendor, long>();
        repo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Vendor>>())).ReturnsAsync(vendor);
        if (vendor is not null)
        {
            // ProcessAcceptedBookingAsync re-loads the vendor by id after a successful accept.
            repo.Setup(r => r.GetAsync(vendor.Id)).ReturnsAsync(vendor);
        }
        return repo;
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

        SetupAgreementToken();

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
            VendorUserId, NotificationTypes.BookingRequestReceived, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);

        Assert.Equal(ClientId, result.ClientId);
    }

    // ---------------- CreateBookingRequestAsync: full-vs-deposit decision (Phase 1, Section B) ----------------

    /// <summary>Wires every repo the create happy-path needs for the given slot and returns the Payment
    /// the service records, so the deposit-decision tests can assert on the authorized amount and split.</summary>
    private async Task<(Payment Payment, AuthorizePaymentIntentRequest AuthorizeRequest, Client Client)> RunCreateAndCapturePaymentAsync(
        VendorAvailability slot, decimal basePrice = PackagePrice, string? existingCustomerId = null)
    {
        var client = CreateClient();
        client.StripeCustomerId = existingCustomerId;
        SetupClientRepo(client);
        var eventPlanRepo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        eventPlanRepo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(CreateEventPlan());

        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>())).ReturnsAsync(slot);

        var userRepo = _unitOfWorkMock.SetupRepository<ApplicationUser, long>();
        userRepo.Setup(r => r.GetAsync(VendorUserId)).ReturnsAsync(CreateVendorUser());
        // The deposit path loads the client's user (for the Stripe Customer's email/name) when it creates one.
        userRepo.Setup(r => r.GetAsync(ClientUserId))
            .ReturnsAsync(new ApplicationUser { Id = ClientUserId, FullName = "Client User", Email = "client@test.local" });

        SetupPackageRepo(basePrice: basePrice);

        // A newly created Stripe Customer resolves to this id; deposit tests assert it lands on the client.
        _paymentGatewayServiceMock
            .Setup(g => g.CreateCustomerAsync(It.IsAny<CreateCustomerRequest>()))
            .ReturnsAsync("cus_new");

        AuthorizePaymentIntentRequest? authorizeRequest = null;
        _paymentGatewayServiceMock
            .Setup(g => g.AuthorizePaymentIntentAsync(It.IsAny<AuthorizePaymentIntentRequest>()))
            .Callback<AuthorizePaymentIntentRequest>(r => authorizeRequest = r)
            .ReturnsAsync(new PaymentIntentResult { PaymentIntentId = "pi_plan", ClientSecret = "secret", Status = "requires_capture" });
        SetupAgreementToken();

        _unitOfWorkMock.SetupRepository<BookingRequest, long>();

        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        Payment? capturedPayment = null;
        paymentRepo.Setup(r => r.AddAsync(It.IsAny<Payment>()))
            .Callback<Payment>(p => capturedPayment = p)
            .Returns(Task.CompletedTask);

        _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();

        var service = CreateService();
        await service.CreateBookingRequestAsync(ClientUserId, CreateValidDto());

        Assert.NotNull(capturedPayment);
        Assert.NotNull(authorizeRequest);
        return (capturedPayment!, authorizeRequest!, client);
    }

    [Fact]
    public async Task CreateBookingRequestAsync_EventBeyondThreshold_AuthorizesDepositAndRecordsDepositPath()
    {
        // 60 days out (> 7-day threshold) => deposit path. Deposit = 20% of 5000 = 1000.
        var slot = CreateSlot(startAt: DateTimeOffset.UtcNow.AddDays(60));

        var (payment, authorizeRequest, _) = await RunCreateAndCapturePaymentAsync(slot);

        Assert.Equal(100000, authorizeRequest.AmountInSmallestUnit); // 1000.00 EGP deposit
        Assert.True(payment.IsDeposit);
        Assert.Equal(1000m, payment.Amount);        // only the deposit is authorized/held
        Assert.Equal(1000m, payment.DepositAmount);
        Assert.Equal(PackagePrice, payment.TotalAmount);
        Assert.Equal(PaymentStatus.DepositAuthorized, payment.Status);
    }

    [Fact]
    public async Task CreateBookingRequestAsync_EventWithinThreshold_AuthorizesFullPayment()
    {
        // 3 days out (<= 7-day threshold) => full-payment path, identical to today's behavior.
        var slot = CreateSlot(startAt: DateTimeOffset.UtcNow.AddDays(3));

        var (payment, authorizeRequest, _) = await RunCreateAndCapturePaymentAsync(slot);

        Assert.Equal(500000, authorizeRequest.AmountInSmallestUnit); // full 5000.00 EGP
        Assert.False(payment.IsDeposit);
        Assert.Equal(PackagePrice, payment.Amount);
        Assert.Null(payment.DepositAmount);
        Assert.Equal(PackagePrice, payment.TotalAmount);
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
    }

    [Fact]
    public async Task CreateBookingRequestAsync_EventExactlyAtThreshold_AuthorizesFullPayment()
    {
        // Exactly 7 days out: the threshold is inclusive, so this stays on the full-payment path.
        var slot = CreateSlot(startAt: DateTimeOffset.UtcNow.AddDays(7));

        var (payment, authorizeRequest, _) = await RunCreateAndCapturePaymentAsync(slot);

        Assert.Equal(500000, authorizeRequest.AmountInSmallestUnit);
        Assert.False(payment.IsDeposit);
        Assert.Equal(PackagePrice, payment.Amount);
    }

    // ---------------- CreateBookingRequestAsync: save card on deposit path (Phase 2, Section A) ----------------

    [Fact]
    public async Task CreateBookingRequestAsync_DepositPath_CreatesCustomerSavesCardAndPersistsCustomerId()
    {
        // Client has no Stripe Customer yet -> one is created, attached to the deposit PI, and the card is
        // saved off-session for the later remainder charge.
        var slot = CreateSlot(startAt: DateTimeOffset.UtcNow.AddDays(60));

        var (payment, authorizeRequest, client) = await RunCreateAndCapturePaymentAsync(slot);

        _paymentGatewayServiceMock.Verify(g => g.CreateCustomerAsync(It.IsAny<CreateCustomerRequest>()), Times.Once);
        Assert.Equal("cus_new", client.StripeCustomerId);           // persisted for reuse
        Assert.Equal("cus_new", authorizeRequest.CustomerId);       // PI attached to the customer
        Assert.True(authorizeRequest.SaveCardForOffSession);         // card saved off-session
        Assert.Equal("pm_card_visa", payment.SavedPaymentMethodId); // saved for the remainder charge
    }

    [Fact]
    public async Task CreateBookingRequestAsync_DepositPath_ReusesExistingCustomer()
    {
        // Client already has a Stripe Customer -> reuse it, do not create another.
        var slot = CreateSlot(startAt: DateTimeOffset.UtcNow.AddDays(60));

        var (payment, authorizeRequest, client) = await RunCreateAndCapturePaymentAsync(slot, existingCustomerId: "cus_existing");

        _paymentGatewayServiceMock.Verify(g => g.CreateCustomerAsync(It.IsAny<CreateCustomerRequest>()), Times.Never);
        Assert.Equal("cus_existing", client.StripeCustomerId);
        Assert.Equal("cus_existing", authorizeRequest.CustomerId);
        Assert.True(authorizeRequest.SaveCardForOffSession);
        Assert.Equal("pm_card_visa", payment.SavedPaymentMethodId);
    }

    [Fact]
    public async Task CreateBookingRequestAsync_FullPaymentPath_DoesNotCreateCustomerOrSaveCard()
    {
        // Full path (event within threshold): no customer, no off-session save, nothing to charge later.
        var slot = CreateSlot(startAt: DateTimeOffset.UtcNow.AddDays(3));

        var (payment, authorizeRequest, client) = await RunCreateAndCapturePaymentAsync(slot);

        _paymentGatewayServiceMock.Verify(g => g.CreateCustomerAsync(It.IsAny<CreateCustomerRequest>()), Times.Never);
        Assert.Null(client.StripeCustomerId);
        Assert.Null(authorizeRequest.CustomerId);
        Assert.False(authorizeRequest.SaveCardForOffSession);
        Assert.Null(payment.SavedPaymentMethodId);
    }

    // ---------------- PayRemainderAsync: client on-session pay-remainder (Phase 3, Section D) ----------------

    /// <summary>Wires the repos/mocks a PayRemainderAsync call needs and returns the tracked payment/booking.</summary>
    private (BookingRequest Booking, Payment Payment) SetupPayRemainder(
        PaymentStatus paymentStatus = PaymentStatus.DepositPaid_RemainderDue,
        bool isDeposit = true,
        string? savedPaymentMethodId = "pm_saved",
        string? stripeCustomerId = "cus_x",
        long bookingClientId = ClientId,
        bool claimSucceeds = true)
    {
        var client = CreateClient();
        client.StripeCustomerId = stripeCustomerId;
        var clientRepo = _unitOfWorkMock.SetupRepository<Client, long>();
        clientRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Client>>())).ReturnsAsync(client);

        var booking = CreateBooking(status: BookingStatus.Accepted, clientId: bookingClientId);
        booking.PaymentStatus = BookingPaymentStatus.DepositPaid;
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(booking);

        var payment = new Payment
        {
            Id = 1,
            BookingRequestId = 1,
            ClientId = ClientId,
            VendorId = VendorId,
            IsDeposit = isDeposit,
            DepositAmount = 1000m,
            TotalAmount = PackagePrice,
            SavedPaymentMethodId = savedPaymentMethodId,
            Status = paymentStatus
        };
        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Payment>>())).ReturnsAsync(payment);

        _unitOfWorkMock
            .Setup(u => u.TryClaimRemainderChargeAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(claimSucceeds);

        _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();
        var vendorRepo = _unitOfWorkMock.SetupRepository<Vendor, long>();
        vendorRepo.Setup(r => r.GetAsync(VendorId)).ReturnsAsync(CreateVendor());

        return (booking, payment);
    }

    private void SetupChargeOnSession(string status, string paymentIntentId = "pi_onsession", string? clientSecret = "cs_test")
    {
        _paymentGatewayServiceMock
            .Setup(g => g.ChargeOnSessionAsync(It.IsAny<ChargeOnSessionRequest>()))
            .ReturnsAsync(new PaymentIntentResult { PaymentIntentId = paymentIntentId, ClientSecret = clientSecret, Status = status });
    }

    [Fact]
    public async Task PayRemainderAsync_EligibleRemainderDue_ChargesAndMarksFullyPaidClearingGrace()
    {
        var (booking, payment) = SetupPayRemainder(PaymentStatus.DepositPaid_RemainderDue);
        payment.RemainderFailedAt = DateTimeOffset.UtcNow; // pretend a prior failure set the grace clock
        ChargeOnSessionRequest? charge = null;
        _paymentGatewayServiceMock.Setup(g => g.ChargeOnSessionAsync(It.IsAny<ChargeOnSessionRequest>()))
            .Callback<ChargeOnSessionRequest>(r => charge = r)
            .ReturnsAsync(new PaymentIntentResult { PaymentIntentId = "pi_onsession", ClientSecret = "cs", Status = "succeeded" });

        var result = await CreateService().PayRemainderAsync(1, ClientUserId);

        Assert.Equal("succeeded", result.Status);
        Assert.False(result.RequiresAction);
        Assert.NotNull(charge);
        Assert.Equal(400000, charge!.AmountInSmallestUnit);            // 5000 - 1000 = 4000.00
        Assert.Equal("remainder-onsession-1", charge.IdempotencyKey);  // client's own stable key
        Assert.Equal("cus_x", charge.CustomerId);
        Assert.Equal("pm_saved", charge.PaymentMethodId);

        Assert.Equal(PaymentStatus.FullyPaid, payment.Status);
        Assert.Equal("pi_onsession", payment.RemainderGatewayReference);
        Assert.Null(payment.RemainderFailedAt);                        // grace cleared
        Assert.Equal(BookingPaymentStatus.Paid, booking.PaymentStatus);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PayRemainderAsync_EligibleRemainderFailed_Succeeds()
    {
        var (booking, payment) = SetupPayRemainder(PaymentStatus.RemainderFailed);
        SetupChargeOnSession("succeeded");

        var result = await CreateService().PayRemainderAsync(1, ClientUserId);

        Assert.Equal("succeeded", result.Status);
        Assert.Equal(PaymentStatus.FullyPaid, payment.Status);
        Assert.Equal(BookingPaymentStatus.Paid, booking.PaymentStatus);
    }

    [Fact]
    public async Task PayRemainderAsync_RequiresAction_ReturnsClientSecretAndStaysCharging()
    {
        var (_, payment) = SetupPayRemainder(PaymentStatus.DepositPaid_RemainderDue);
        SetupChargeOnSession("requires_action", paymentIntentId: "pi_sca", clientSecret: "cs_sca");

        var result = await CreateService().PayRemainderAsync(1, ClientUserId);

        Assert.True(result.RequiresAction);
        Assert.Equal("requires_action", result.Status);
        Assert.Equal("cs_sca", result.ClientSecret);
        // Stays claimed (RemainderCharging) with the remainder PI id stored so the webhook can finalize it.
        Assert.Equal(PaymentStatus.RemainderCharging, payment.Status);
        Assert.Equal("pi_sca", payment.RemainderGatewayReference);
    }

    [Fact]
    public async Task PayRemainderAsync_IneligibleState_ThrowsBadRequest()
    {
        SetupPayRemainder(PaymentStatus.FullyPaid);

        await Assert.ThrowsAsync<BadRequestExeption>(() => CreateService().PayRemainderAsync(1, ClientUserId));
        _paymentGatewayServiceMock.Verify(g => g.ChargeOnSessionAsync(It.IsAny<ChargeOnSessionRequest>()), Times.Never);
    }

    [Fact]
    public async Task PayRemainderAsync_NotOwner_ThrowsNotFound()
    {
        SetupPayRemainder(bookingClientId: ClientId + 999); // booking belongs to a different client

        await Assert.ThrowsAsync<NotFoundExeption>(() => CreateService().PayRemainderAsync(1, ClientUserId));
        _paymentGatewayServiceMock.Verify(g => g.ChargeOnSessionAsync(It.IsAny<ChargeOnSessionRequest>()), Times.Never);
    }

    [Fact]
    public async Task PayRemainderAsync_FullPaymentPath_ThrowsBadRequest()
    {
        SetupPayRemainder(isDeposit: false); // full-payment booking has no remainder

        await Assert.ThrowsAsync<BadRequestExeption>(() => CreateService().PayRemainderAsync(1, ClientUserId));
        _paymentGatewayServiceMock.Verify(g => g.ChargeOnSessionAsync(It.IsAny<ChargeOnSessionRequest>()), Times.Never);
    }

    [Fact]
    public async Task PayRemainderAsync_CannotClaim_ThrowsBadRequestAndDoesNotCharge()
    {
        // Interleaving: the background job already claimed this payment -> client can't, and must not charge.
        SetupPayRemainder(PaymentStatus.DepositPaid_RemainderDue, claimSucceeds: false);

        await Assert.ThrowsAsync<BadRequestExeption>(() => CreateService().PayRemainderAsync(1, ClientUserId));
        _paymentGatewayServiceMock.Verify(g => g.ChargeOnSessionAsync(It.IsAny<ChargeOnSessionRequest>()), Times.Never);
    }

    [Fact]
    public async Task PayRemainderAsync_ChargeDeclined_TransitionsRemainderFailedAndThrows()
    {
        var (booking, payment) = SetupPayRemainder(PaymentStatus.DepositPaid_RemainderDue);
        _paymentGatewayServiceMock.Setup(g => g.ChargeOnSessionAsync(It.IsAny<ChargeOnSessionRequest>()))
            .ThrowsAsync(new PaymentDeclinedExeption("Your card was declined."));

        await Assert.ThrowsAsync<PaymentDeclinedExeption>(() => CreateService().PayRemainderAsync(1, ClientUserId));

        Assert.Equal(PaymentStatus.RemainderFailed, payment.Status);
        Assert.NotNull(payment.RemainderFailedAt);
        Assert.Equal(BookingPaymentStatus.RemainderFailed, booking.PaymentStatus);
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

        var hold = CreateSlot(status: AvailabilityStatus.Held);
        hold.BookingRequestId = 1;
        hold.BookingRequest = booking;
        hold.HoldExpiresAt = DateTimeOffset.UtcNow.AddHours(24);
        var holds = new List<VendorAvailability> { hold };
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
        Assert.Equal(AvailabilityStatus.Available, hold.Status);
        Assert.Null(hold.BookingRequestId);
        Assert.Null(hold.BookingRequest);
        Assert.Null(hold.HoldExpiresAt);
        slotRepo.Verify(r => r.Update(hold), Times.Once);
        slotRepo.Verify(r => r.DeleteRange(It.IsAny<IEnumerable<VendorAvailability>>()), Times.Never);
        Assert.NotNull(capturedHistory);
        Assert.Equal("Pending", capturedHistory!.PreviousStatus);
        Assert.Equal("Cancelled", capturedHistory.NewStatus);

        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _paymentGatewayServiceMock.Verify(g => g.CancelPaymentIntentAsync(It.Is<CancelPaymentIntentRequest>(
            r => r.PaymentIntentId == "pi_to_void")), Times.Once);
        Assert.Equal(PaymentStatus.Cancelled, payment.Status);
        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            VendorUserId, NotificationTypes.BookingCancelled, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);

        Assert.Equal(BookingStatus.Cancelled, result.Status);
    }

    // ---------------- RequestCancellationAsync: deposit-aware manual cancel (Phase 3, Section G) ----------------

    private (BookingRequest Booking, VendorAvailability Hold) SetupRequestCancellation(Payment payment)
    {
        SetupClientRepo(CreateClient());
        var booking = CreateBooking(status: BookingStatus.Accepted);
        booking.PaymentStatus = BookingPaymentStatus.DepositPaid;
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(booking);

        var hold = CreateSlot(status: AvailabilityStatus.Booked);
        hold.BookingRequestId = 1;
        hold.BookingRequest = booking;
        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<VendorAvailability> { hold });

        _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();
        var vendorRepo = _unitOfWorkMock.SetupRepository<Vendor, long>();
        vendorRepo.Setup(r => r.GetAsync(VendorId)).ReturnsAsync(CreateVendor());

        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Payment>>())).ReturnsAsync(payment);

        return (booking, hold);
    }

    private static Payment DepositPayment(PaymentStatus status) => new()
    {
        Id = 1,
        BookingRequestId = 1,
        ClientId = ClientId,
        VendorId = VendorId,
        IsDeposit = true,
        DepositAmount = 1000m,
        TotalAmount = PackagePrice,
        Status = status
    };

    [Fact]
    public async Task RequestCancellationAsync_DepositOnlyRemainderDue_ImmediateForfeitCancelReleasesSlotNoAdmin()
    {
        var (booking, hold) = SetupRequestCancellation(DepositPayment(PaymentStatus.DepositPaid_RemainderDue));

        var result = await CreateService().RequestCancellationAsync(1, ClientUserId, "changed my mind");

        // Immediate cancel + slot released + deposit forfeited (no refund), NO admin review.
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.NotNull(booking.CancelledAt);
        Assert.Equal(RefundStatus.None, booking.RefundStatus);
        Assert.Equal(0m, booking.CancellationRefundAmount);
        Assert.Equal(AvailabilityStatus.Available, hold.Status);
        Assert.Null(hold.BookingRequestId);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);

        // No admin review entry, no Stripe refund.
        _notificationServiceMock.Verify(n => n.NotifyRoleAsync(
            Roles.Admin, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
        _paymentGatewayServiceMock.Verify(g => g.RefundPaymentIntentAsync(It.IsAny<RefundPaymentIntentRequest>()), Times.Never);
        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            VendorUserId, NotificationTypes.BookingCancelled, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
        Assert.Equal(BookingStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task RequestCancellationAsync_RemainderFailed_ImmediateForfeitCancel()
    {
        var (booking, hold) = SetupRequestCancellation(DepositPayment(PaymentStatus.RemainderFailed));

        await CreateService().RequestCancellationAsync(1, ClientUserId, "no longer needed");

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(RefundStatus.None, booking.RefundStatus);
        Assert.Equal(AvailabilityStatus.Available, hold.Status);
        _notificationServiceMock.Verify(n => n.NotifyRoleAsync(
            Roles.Admin, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task RequestCancellationAsync_FullyPaidDeposit_GoesToAdminReview()
    {
        var (booking, hold) = SetupRequestCancellation(DepositPayment(PaymentStatus.FullyPaid));

        await CreateService().RequestCancellationAsync(1, ClientUserId, "cancel please");

        // Unchanged admin-review path: request only — not cancelled, slot NOT released, admin notified.
        Assert.Equal(BookingStatus.CancellationRequested, booking.Status);
        Assert.Equal(RefundStatus.PendingReview, booking.RefundStatus);
        Assert.Equal(AvailabilityStatus.Booked, hold.Status); // slot untouched, awaits admin
        _notificationServiceMock.Verify(n => n.NotifyRoleAsync(
            Roles.Admin, NotificationTypes.BookingCancellationRequested, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task RequestCancellationAsync_FullPaymentPath_GoesToAdminReview()
    {
        // Full-payment booking: no deposit payment on file (IsDeposit=false / not found) -> review path.
        var completed = DepositPayment(PaymentStatus.Completed);
        completed.IsDeposit = false;
        var (booking, _) = SetupRequestCancellation(completed);

        await CreateService().RequestCancellationAsync(1, ClientUserId, "cancel please");

        Assert.Equal(BookingStatus.CancellationRequested, booking.Status);
        Assert.Equal(RefundStatus.PendingReview, booking.RefundStatus);
    }

    [Fact]
    public async Task RequestCancellationAsync_RemainderCharging_Rejected()
    {
        SetupRequestCancellation(DepositPayment(PaymentStatus.RemainderCharging));

        await Assert.ThrowsAsync<BadRequestExeption>(() =>
            CreateService().RequestCancellationAsync(1, ClientUserId, "cancel please"));
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCancellationQuoteAsync_DepositOnly_ReturnsZeroRefund()
    {
        SetupClientRepo(CreateClient());
        var booking = CreateBooking(status: BookingStatus.Accepted);
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(booking);
        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Payment>>()))
            .ReturnsAsync(DepositPayment(PaymentStatus.DepositPaid_RemainderDue));

        var quote = await CreateService().GetCancellationQuoteAsync(1, ClientUserId);

        Assert.Equal(0m, quote.RefundPercent);
        Assert.Equal(0m, quote.RefundAmount);
    }

    // ---------------- AcceptBookingRequestAsync ----------------

    [Fact]
    public async Task AcceptBookingRequestAsync_BookingNotFound_ThrowsNotFound()
    {
        SetupVendorUserRepo(CreateVendor());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync((BookingRequest?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.AcceptBookingRequestAsync(1, VendorUserId, true));
    }

    [Fact]
    public async Task AcceptBookingRequestAsync_OwnedByAnotherVendor_ThrowsNotFound()
    {
        SetupVendorUserRepo(new Vendor { Id = 999, UserId = VendorUserId, BusinessName = "Other" });
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(CreateBooking());

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.AcceptBookingRequestAsync(1, VendorUserId, true));
    }

    [Fact]
    public async Task AcceptBookingRequestAsync_NotPending_ThrowsBadRequest()
    {
        SetupVendorUserRepo(CreateVendor());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(CreateBooking(status: BookingStatus.Accepted));

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.AcceptBookingRequestAsync(1, VendorUserId, true));
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

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.AcceptBookingRequestAsync(1, VendorUserId, true));
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

        // ProcessAcceptedBookingAsync loads the vendor's user (for the one-time partnership agreement).
        _unitOfWorkMock.SetupRepository<ApplicationUser, long>();

        var service = CreateService();
        var result = await service.AcceptBookingRequestAsync(1, VendorUserId, true);

        Assert.Equal(BookingStatus.Accepted, booking.Status);
        Assert.Equal(BookingPaymentStatus.Paid, booking.PaymentStatus);
        Assert.NotNull(booking.RespondedAt);
        Assert.NotNull(booking.VendorAgreedAt);

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
            ClientUserId, NotificationTypes.BookingAccepted, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);

        Assert.Equal(BookingStatus.Accepted, result.Status);
    }

    [Fact]
    public async Task AcceptBookingRequestAsync_DepositPath_CapturesDepositAndRestsAtRemainderDue()
    {
        SetupVendorUserRepo(CreateVendor());
        var booking = CreateBooking();
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(booking);

        // Deposit-path payment: only the deposit (1000) was authorized/held; total is 5000.
        var payment = CreateAuthorizedPayment("pi_deposit_accept");
        payment.Status = PaymentStatus.DepositAuthorized;
        payment.IsDeposit = true;
        payment.Amount = 1000m;
        payment.DepositAmount = 1000m;
        payment.TotalAmount = PackagePrice;
        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Payment>>())).ReturnsAsync(payment);

        _paymentGatewayServiceMock
            .Setup(g => g.CapturePaymentIntentAsync(It.IsAny<CapturePaymentIntentRequest>()))
            .ReturnsAsync(new PaymentIntentResult { PaymentIntentId = "pi_deposit_accept", ClientSecret = "secret", Status = "succeeded" });

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
        _unitOfWorkMock.SetupRepository<ApplicationUser, long>();

        var service = CreateService();
        var result = await service.AcceptBookingRequestAsync(1, VendorUserId, true);

        // Booking is accepted but rests at "deposit paid, remainder due" — NOT fully paid.
        Assert.Equal(BookingStatus.Accepted, booking.Status);
        Assert.Equal(BookingPaymentStatus.DepositPaid, booking.PaymentStatus);

        Assert.Equal(PaymentStatus.DepositPaid_RemainderDue, payment.Status);
        Assert.NotNull(payment.PaidAt);
        // The split is untouched by accept: the remainder is still recorded, just not collected.
        Assert.Equal(1000m, payment.DepositAmount);
        Assert.Equal(PackagePrice, payment.TotalAmount);

        Assert.Equal(AvailabilityStatus.Booked, hold.Status);

        Assert.NotNull(capturedHistory);
        Assert.Contains("deposit", capturedHistory!.Notes, StringComparison.OrdinalIgnoreCase);

        // Capture is the same call as the full path — it captures exactly what was held (the deposit).
        _paymentGatewayServiceMock.Verify(g => g.CapturePaymentIntentAsync(It.Is<CapturePaymentIntentRequest>(
            r => r.PaymentIntentId == "pi_deposit_accept")), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        // Remainder is NOT charged in Phase 1: no second authorize/capture happens.
        _paymentGatewayServiceMock.Verify(g => g.AuthorizePaymentIntentAsync(It.IsAny<AuthorizePaymentIntentRequest>()), Times.Never);

        Assert.Equal(BookingStatus.Accepted, result.Status);
    }

    [Fact]
    public async Task AcceptBookingRequestAsync_CaptureFails_AutoDeclinesVoidsAuthorizationResetsHoldAndThrowsPaymentDeclined()
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

        var hold = CreateSlot(status: AvailabilityStatus.Held);
        hold.BookingRequestId = 1;
        hold.BookingRequest = booking;
        hold.HoldExpiresAt = DateTimeOffset.UtcNow.AddHours(24);
        var holdRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        holdRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<VendorAvailability> { hold });

        _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();

        var clientRepo = _unitOfWorkMock.SetupRepository<Client, long>();
        clientRepo.Setup(r => r.GetAsync(ClientId)).ReturnsAsync(CreateClient());

        var service = CreateService();

        await Assert.ThrowsAsync<PaymentDeclinedExeption>(() => service.AcceptBookingRequestAsync(1, VendorUserId, true));

        Assert.Equal(BookingStatus.Rejected, booking.Status);

        // BUG 1: the still-live authorization must actually be voided with Stripe, not just marked
        // Failed locally — this is what left pi_3Tu1cdJTBgcTyrCL0osgfeOY orphaned on the test account.
        _paymentGatewayServiceMock.Verify(g => g.CancelPaymentIntentAsync(It.Is<CancelPaymentIntentRequest>(
            r => r.PaymentIntentId == "pi_capture_fail")), Times.Once);
        Assert.Equal(PaymentStatus.Cancelled, payment.Status);

        // BUG 2: the hold must be released back to Available, not deleted, so the slot is rebookable.
        Assert.Equal(AvailabilityStatus.Available, hold.Status);
        Assert.Null(hold.BookingRequestId);
        Assert.Null(hold.BookingRequest);
        Assert.Null(hold.HoldExpiresAt);
        holdRepo.Verify(r => r.Update(hold), Times.Once);
        holdRepo.Verify(r => r.DeleteRange(It.IsAny<IEnumerable<VendorAvailability>>()), Times.Never);
        holdRepo.Verify(r => r.Delete(It.IsAny<VendorAvailability>()), Times.Never);

        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            ClientUserId, NotificationTypes.BookingRejected, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task AcceptBookingRequestAsync_CaptureFailsAndVoidAlsoFails_StillAutoDeclinesAndResetsHold()
    {
        SetupVendorUserRepo(CreateVendor());
        var booking = CreateBooking();
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(booking);

        var payment = CreateAuthorizedPayment("pi_capture_fail_void_fail");
        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Payment>>())).ReturnsAsync(payment);

        _paymentGatewayServiceMock
            .Setup(g => g.CapturePaymentIntentAsync(It.IsAny<CapturePaymentIntentRequest>()))
            .ThrowsAsync(new InvalidOperationException("card no longer valid"));
        _paymentGatewayServiceMock
            .Setup(g => g.CancelPaymentIntentAsync(It.IsAny<CancelPaymentIntentRequest>()))
            .ThrowsAsync(new InvalidOperationException("stripe unreachable"));

        var hold = CreateSlot(status: AvailabilityStatus.Held);
        hold.BookingRequestId = 1;
        hold.BookingRequest = booking;
        var holdRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        holdRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<VendorAvailability> { hold });

        _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();

        var clientRepo = _unitOfWorkMock.SetupRepository<Client, long>();
        clientRepo.Setup(r => r.GetAsync(ClientId)).ReturnsAsync(CreateClient());

        var service = CreateService();

        await Assert.ThrowsAsync<PaymentDeclinedExeption>(() => service.AcceptBookingRequestAsync(1, VendorUserId, true));

        Assert.Equal(BookingStatus.Rejected, booking.Status);
        Assert.Equal(PaymentStatus.Failed, payment.Status); // void failed, so it stays at the pre-void Failed status
        Assert.Equal(AvailabilityStatus.Available, hold.Status); // hold reset still happens regardless of void outcome
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

        var hold = CreateSlot(status: AvailabilityStatus.Held);
        hold.BookingRequestId = 1;
        hold.BookingRequest = booking;
        hold.HoldExpiresAt = DateTimeOffset.UtcNow.AddHours(24);
        var holds = new List<VendorAvailability> { hold };
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
        Assert.Equal(AvailabilityStatus.Available, hold.Status);
        Assert.Null(hold.BookingRequestId);
        Assert.Null(hold.BookingRequest);
        Assert.Null(hold.HoldExpiresAt);
        slotRepo.Verify(r => r.Update(hold), Times.Once);
        slotRepo.Verify(r => r.DeleteRange(It.IsAny<IEnumerable<VendorAvailability>>()), Times.Never);

        Assert.NotNull(capturedHistory);
        Assert.Equal("Pending", capturedHistory!.PreviousStatus);
        Assert.Equal("Rejected", capturedHistory.NewStatus);
        Assert.Equal(VendorUserId, capturedHistory.ChangedByUserId);

        _paymentGatewayServiceMock.Verify(g => g.CancelPaymentIntentAsync(It.Is<CancelPaymentIntentRequest>(
            r => r.PaymentIntentId == "pi_reject_test")), Times.Once);
        Assert.Equal(PaymentStatus.Cancelled, payment.Status);

        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            ClientUserId, NotificationTypes.BookingRejected, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);

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

    // ---------------- ListVendorBookingRequestsAsync ----------------

    [Fact]
    public async Task ListVendorBookingRequestsAsync_Valid_ReturnsPagedResult()
    {
        SetupVendorUserRepo(CreateVendor());
        var bookings = new List<BookingRequest> { CreateBooking() };

        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetCountAsync(It.IsAny<ISpecification<BookingRequest>>())).ReturnsAsync(1);
        bookingRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<BookingRequest>>(), It.IsAny<bool>()))
            .ReturnsAsync(bookings);

        var service = CreateService();
        var result = await service.ListVendorBookingRequestsAsync(VendorUserId, new BookingRequestFilterDto());

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(VendorId, result.Items[0].VendorId);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public async Task ListVendorBookingRequestsAsync_VendorNotFound_ThrowsNotFound()
    {
        SetupVendorUserRepo(null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(
            () => service.ListVendorBookingRequestsAsync(VendorUserId, new BookingRequestFilterDto()));
    }

    // ---------------- GetVendorBookingRequestAsync ----------------

    [Fact]
    public async Task GetVendorBookingRequestAsync_NotFound_ThrowsNotFound()
    {
        SetupVendorUserRepo(CreateVendor());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync((BookingRequest?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.GetVendorBookingRequestAsync(1, VendorUserId));
    }

    [Fact]
    public async Task GetVendorBookingRequestAsync_OwnedByAnotherVendor_ThrowsNotFound()
    {
        SetupVendorUserRepo(new Vendor { Id = 999, UserId = VendorUserId, BusinessName = "Other" });
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(CreateBooking());

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.GetVendorBookingRequestAsync(1, VendorUserId));
    }

    [Fact]
    public async Task GetVendorBookingRequestAsync_Valid_ReturnsDto()
    {
        SetupVendorUserRepo(CreateVendor());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(1L)).ReturnsAsync(CreateBooking());

        var service = CreateService();
        var result = await service.GetVendorBookingRequestAsync(1, VendorUserId);

        Assert.Equal(1, result.Id);
        Assert.Equal(VendorId, result.VendorId);
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
