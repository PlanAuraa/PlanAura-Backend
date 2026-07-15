using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Planura.Core.Application.Abstraction.PaymentGateway;
using Planura.Core.Application.Common;
using Planura.Core.Application.Mappings;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;
using Planura.Tests.TestHelpers;
using Xunit;

namespace Planura.Tests.Services;

public class PaymentServiceTests
{
    private const long ClientUserId = 500;
    private const long ClientId = 10;
    private const long VendorId = 20;
    private const long VendorUserId = 200;
    private const long BookingRequestId = 60;

    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Mock<IPaymentGatewayService> _paymentGatewayServiceMock = new();
    private readonly IMapper _mapper = new MapperConfiguration(
        cfg => cfg.AddProfile<MappingProfile>(),
        NullLoggerFactory.Instance).CreateMapper();

    private PaymentService CreateService() => new(
        _unitOfWorkMock.Object,
        _mapper,
        _notificationServiceMock.Object,
        _paymentGatewayServiceMock.Object,
        Options.Create(new StripeOptions
        {
            SecretKey = "sk_test_x",
            PublishableKey = "pk_test_x",
            WebhookSecret = "whsec_x",
            DefaultCurrency = "EGP"
        }));

    private static Client CreateClient() => new() { Id = ClientId, UserId = ClientUserId };

    private static BookingRequest CreateBooking(
        BookingStatus status = BookingStatus.Accepted,
        BookingPaymentStatus paymentStatus = BookingPaymentStatus.Unpaid,
        decimal? agreedPrice = 5000m,
        long clientId = ClientId) => new()
    {
        Id = BookingRequestId,
        ClientId = clientId,
        VendorId = VendorId,
        EventPlanId = 1,
        EventDate = new DateOnly(2026, 8, 1),
        Status = status,
        PaymentStatus = paymentStatus,
        AgreedPrice = agreedPrice
    };

    private static Payment CreatePayment(
        long id = 1,
        PaymentStatus status = PaymentStatus.Pending,
        string? gatewayReference = "pi_123") => new()
    {
        Id = id,
        BookingRequestId = BookingRequestId,
        ClientId = ClientId,
        VendorId = VendorId,
        Amount = 5000m,
        Status = status,
        GatewayReference = gatewayReference
    };

    private void SetupClientRepo(Client? client)
    {
        var repo = _unitOfWorkMock.SetupRepository<Client, long>();
        repo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Client>>())).ReturnsAsync(client);
    }

    // ---------------- GetPaymentOptionsAsync ----------------

    [Fact]
    public async Task GetPaymentOptionsAsync_BookingNotFound_ThrowsNotFound()
    {
        SetupClientRepo(CreateClient());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(BookingRequestId)).ReturnsAsync((BookingRequest?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.GetPaymentOptionsAsync(BookingRequestId, ClientUserId));
    }

    [Fact]
    public async Task GetPaymentOptionsAsync_NotAcceptedYet_IsPayableFalse()
    {
        SetupClientRepo(CreateClient());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(BookingRequestId)).ReturnsAsync(CreateBooking(status: BookingStatus.Pending));

        var service = CreateService();
        var result = await service.GetPaymentOptionsAsync(BookingRequestId, ClientUserId);

        Assert.False(result.IsPayable);
    }

    [Fact]
    public async Task GetPaymentOptionsAsync_AcceptedAndUnpaid_IsPayableTrue()
    {
        SetupClientRepo(CreateClient());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(BookingRequestId)).ReturnsAsync(CreateBooking());

        var service = CreateService();
        var result = await service.GetPaymentOptionsAsync(BookingRequestId, ClientUserId);

        Assert.True(result.IsPayable);
        Assert.Equal(5000m, result.AmountDue);
        Assert.Equal("pk_test_x", result.PublishableKey);
    }

    // ---------------- InitiatePaymentAsync ----------------

    [Fact]
    public async Task InitiatePaymentAsync_BookingOwnedByAnotherClient_ThrowsNotFound()
    {
        SetupClientRepo(CreateClient());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(BookingRequestId)).ReturnsAsync(CreateBooking(clientId: 999));

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(
            () => service.InitiatePaymentAsync(BookingRequestId, ClientUserId, new InitiatePaymentDto()));
    }

    [Fact]
    public async Task InitiatePaymentAsync_NotAccepted_ThrowsBadRequest()
    {
        SetupClientRepo(CreateClient());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(BookingRequestId)).ReturnsAsync(CreateBooking(status: BookingStatus.Pending));

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(
            () => service.InitiatePaymentAsync(BookingRequestId, ClientUserId, new InitiatePaymentDto()));
    }

    [Fact]
    public async Task InitiatePaymentAsync_AlreadyPaid_ThrowsBadRequest()
    {
        SetupClientRepo(CreateClient());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(BookingRequestId))
            .ReturnsAsync(CreateBooking(paymentStatus: BookingPaymentStatus.Paid));

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(
            () => service.InitiatePaymentAsync(BookingRequestId, ClientUserId, new InitiatePaymentDto()));
    }

    [Fact]
    public async Task InitiatePaymentAsync_NoAgreedPrice_ThrowsBadRequest()
    {
        SetupClientRepo(CreateClient());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(BookingRequestId)).ReturnsAsync(CreateBooking(agreedPrice: null));

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(
            () => service.InitiatePaymentAsync(BookingRequestId, ClientUserId, new InitiatePaymentDto()));
    }

    [Fact]
    public async Task InitiatePaymentAsync_ExistingPendingPayment_ThrowsBadRequest()
    {
        SetupClientRepo(CreateClient());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(BookingRequestId)).ReturnsAsync(CreateBooking());

        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Payment>>()))
            .ReturnsAsync(CreatePayment());

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(
            () => service.InitiatePaymentAsync(BookingRequestId, ClientUserId, new InitiatePaymentDto()));
    }

    [Fact]
    public async Task InitiatePaymentAsync_Valid_CreatesPaymentAndReturnsClientSecret()
    {
        SetupClientRepo(CreateClient());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(BookingRequestId)).ReturnsAsync(CreateBooking());

        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Payment>>())).ReturnsAsync((Payment?)null);

        Payment? captured = null;
        paymentRepo.Setup(r => r.AddAsync(It.IsAny<Payment>()))
            .Callback<Payment>(p =>
            {
                p.Id = 77;
                captured = p;
            })
            .Returns(Task.CompletedTask);

        _paymentGatewayServiceMock
            .Setup(g => g.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentRequest>()))
            .ReturnsAsync(new PaymentIntentResult { PaymentIntentId = "pi_abc", ClientSecret = "secret_abc" });

        var service = CreateService();
        var result = await service.InitiatePaymentAsync(BookingRequestId, ClientUserId, new InitiatePaymentDto());

        Assert.NotNull(captured);
        Assert.Equal(PaymentStatus.Pending, captured!.Status);
        Assert.Equal("pi_abc", captured.GatewayReference);
        Assert.Equal("secret_abc", result.ClientSecret);
        Assert.Equal(5000m, result.Amount);
        Assert.Equal("EGP", result.Currency);

        _paymentGatewayServiceMock.Verify(g => g.CreatePaymentIntentAsync(It.Is<CreatePaymentIntentRequest>(
            r => r.AmountInSmallestUnit == 500000
                && r.Currency == "egp"
                && r.Metadata["booking_request_id"] == BookingRequestId.ToString()
                && r.Metadata["payment_id"] == "77")), Times.Once);
    }

    [Fact]
    public async Task InitiatePaymentAsync_GatewayThrows_MarksPaymentFailedAndRethrows()
    {
        SetupClientRepo(CreateClient());
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(BookingRequestId)).ReturnsAsync(CreateBooking());

        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Payment>>())).ReturnsAsync((Payment?)null);

        Payment? captured = null;
        paymentRepo.Setup(r => r.AddAsync(It.IsAny<Payment>()))
            .Callback<Payment>(p =>
            {
                p.Id = 77;
                captured = p;
            })
            .Returns(Task.CompletedTask);

        _paymentGatewayServiceMock
            .Setup(g => g.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentRequest>()))
            .ThrowsAsync(new InvalidOperationException("Stripe unreachable"));

        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.InitiatePaymentAsync(BookingRequestId, ClientUserId, new InitiatePaymentDto()));

        Assert.Equal(PaymentStatus.Failed, captured!.Status);
    }

    // ---------------- HandleStripeWebhookAsync ----------------

    [Fact]
    public async Task HandleStripeWebhookAsync_UnknownPaymentIntent_NoOp()
    {
        _paymentGatewayServiceMock
            .Setup(g => g.ConstructWebhookEvent("json", "sig"))
            .Returns(new PaymentGatewayEvent { Type = "payment_intent.succeeded", PaymentIntentId = "pi_unknown" });

        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Payment>>())).ReturnsAsync((Payment?)null);

        var service = CreateService();
        await service.HandleStripeWebhookAsync("json", "sig");

        paymentRepo.Verify(r => r.Update(It.IsAny<Payment>()), Times.Never);
    }

    [Fact]
    public async Task HandleStripeWebhookAsync_PaymentSucceeded_CompletesPaymentAndMarksBookingPaid()
    {
        var payment = CreatePayment();
        _paymentGatewayServiceMock
            .Setup(g => g.ConstructWebhookEvent("json", "sig"))
            .Returns(new PaymentGatewayEvent { Type = "payment_intent.succeeded", PaymentIntentId = "pi_123" });

        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Payment>>())).ReturnsAsync(payment);

        var booking = CreateBooking();
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(BookingRequestId)).ReturnsAsync(booking);

        var clientRepo = _unitOfWorkMock.SetupRepository<Client, long>();
        clientRepo.Setup(r => r.GetAsync(ClientId)).ReturnsAsync(CreateClient());

        var vendorRepo = _unitOfWorkMock.SetupRepository<Vendor, long>();
        vendorRepo.Setup(r => r.GetAsync(VendorId)).ReturnsAsync(new Vendor { Id = VendorId, UserId = VendorUserId, BusinessName = "V" });

        var service = CreateService();
        await service.HandleStripeWebhookAsync("json", "sig");

        Assert.Equal(PaymentStatus.Completed, payment.Status);
        Assert.NotNull(payment.PaidAt);
        Assert.Equal(BookingPaymentStatus.Paid, booking.PaymentStatus);

        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            ClientUserId, "payment_successful", It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            VendorUserId, "payment_received", It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task HandleStripeWebhookAsync_PaymentFailed_MarksPaymentFailed()
    {
        var payment = CreatePayment();
        _paymentGatewayServiceMock
            .Setup(g => g.ConstructWebhookEvent("json", "sig"))
            .Returns(new PaymentGatewayEvent { Type = "payment_intent.payment_failed", PaymentIntentId = "pi_123" });

        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Payment>>())).ReturnsAsync(payment);

        var clientRepo = _unitOfWorkMock.SetupRepository<Client, long>();
        clientRepo.Setup(r => r.GetAsync(ClientId)).ReturnsAsync(CreateClient());

        var service = CreateService();
        await service.HandleStripeWebhookAsync("json", "sig");

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            ClientUserId, "payment_failed", It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task HandleStripeWebhookAsync_ChargeRefunded_MarksPaymentAndBookingRefunded()
    {
        var payment = CreatePayment(status: PaymentStatus.Completed);
        _paymentGatewayServiceMock
            .Setup(g => g.ConstructWebhookEvent("json", "sig"))
            .Returns(new PaymentGatewayEvent { Type = "charge.refunded", PaymentIntentId = "pi_123" });

        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Payment>>())).ReturnsAsync(payment);

        var booking = CreateBooking(paymentStatus: BookingPaymentStatus.Paid);
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(BookingRequestId)).ReturnsAsync(booking);

        var service = CreateService();
        await service.HandleStripeWebhookAsync("json", "sig");

        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.NotNull(payment.RefundedAt);
        Assert.Equal(BookingPaymentStatus.Refunded, booking.PaymentStatus);
    }

    [Fact]
    public async Task HandleStripeWebhookAsync_UnhandledEventType_NoOp()
    {
        var payment = CreatePayment();
        _paymentGatewayServiceMock
            .Setup(g => g.ConstructWebhookEvent("json", "sig"))
            .Returns(new PaymentGatewayEvent { Type = "customer.created", PaymentIntentId = null });

        var service = CreateService();
        await service.HandleStripeWebhookAsync("json", "sig");

        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public async Task HandleStripeWebhookAsync_InvalidSignature_PropagatesBadRequest()
    {
        _paymentGatewayServiceMock
            .Setup(g => g.ConstructWebhookEvent("json", "bad-sig"))
            .Throws(new BadRequestExeption("Invalid Stripe webhook signature: test"));

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.HandleStripeWebhookAsync("json", "bad-sig"));
    }

    // ---------------- ListMyTransactionsAsync ----------------

    [Fact]
    public async Task ListMyTransactionsAsync_Valid_ReturnsPagedResult()
    {
        SetupClientRepo(CreateClient());
        var payments = new List<Payment> { CreatePayment() };

        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetCountAsync(It.IsAny<ISpecification<Payment>>())).ReturnsAsync(1);
        paymentRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Payment>>(), It.IsAny<bool>()))
            .ReturnsAsync(payments);

        var service = CreateService();
        var result = await service.ListMyTransactionsAsync(ClientUserId, new TransactionsFilterDto());

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
    }
}
