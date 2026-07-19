using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Planura.Core.Application.Abstraction.PaymentGateway;
using Planura.Core.Application.Services;
using Planura.Core.Application.Services.BookingHoldExpiryJob;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using Planura.Tests.TestHelpers;
using Xunit;

namespace Planura.Tests.Services;

public class BookingHoldExpiryJobTests
{
    private const long ClientId = 10;
    private const long ClientUserId = 500;
    private const long VendorId = 20;
    private const long VendorUserId = 200;
    private const long BookingRequestId = 1;
    private const long AvailabilityId = 40;

    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Mock<IPaymentGatewayService> _paymentGatewayServiceMock = new();

    private BookingHoldExpiryJob CreateJob() => new(
        _unitOfWorkMock.Object,
        _notificationServiceMock.Object,
        _paymentGatewayServiceMock.Object,
        NullLogger<BookingHoldExpiryJob>.Instance);

    private static Payment CreateAuthorizedPayment() => new()
    {
        Id = 1,
        BookingRequestId = BookingRequestId,
        ClientId = ClientId,
        VendorId = VendorId,
        Amount = 5000m,
        Status = PaymentStatus.Authorized,
        GatewayReference = "pi_expired_hold"
    };

    private static BookingRequest CreateBooking(BookingStatus status = BookingStatus.Pending) => new()
    {
        Id = BookingRequestId,
        ClientId = ClientId,
        VendorId = VendorId,
        EventPlanId = 1,
        EventDate = new DateOnly(2026, 8, 1),
        Status = status
    };

    private static VendorAvailability CreateExpiredHold(BookingRequest? booking) => new()
    {
        Id = AvailabilityId,
        VendorId = VendorId,
        StartAt = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero),
        EndAt = new DateTimeOffset(2026, 8, 1, 14, 0, 0, TimeSpan.Zero),
        Status = AvailabilityStatus.Held,
        HoldExpiresAt = DateTimeOffset.UtcNow.AddHours(-1),
        BookingRequestId = booking?.Id,
        BookingRequest = booking
    };

    [Fact]
    public async Task RunAsync_NoExpiredHolds_NoOp()
    {
        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<VendorAvailability>());

        var job = CreateJob();
        await job.RunAsync();

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_ExpiredHoldWithNonPendingBooking_SkipsIt()
    {
        var booking = CreateBooking(status: BookingStatus.Cancelled);
        var hold = CreateExpiredHold(booking);

        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<VendorAvailability> { hold });

        var job = CreateJob();
        await job.RunAsync();

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_ExpiredHoldWithPendingBooking_ExpiresBookingResetsHoldToAvailableAndNotifiesBothParties()
    {
        var booking = CreateBooking();
        var hold = CreateExpiredHold(booking);

        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<VendorAvailability> { hold });

        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();

        var historyRepo = _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();
        BookingStatusHistory? capturedHistory = null;
        historyRepo.Setup(r => r.AddAsync(It.IsAny<BookingStatusHistory>()))
            .Callback<BookingStatusHistory>(h => capturedHistory = h)
            .Returns(Task.CompletedTask);

        var clientRepo = _unitOfWorkMock.SetupRepository<Client, long>();
        clientRepo.Setup(r => r.GetAsync(ClientId)).ReturnsAsync(new Client { Id = ClientId, UserId = ClientUserId });

        var vendorRepo = _unitOfWorkMock.SetupRepository<Vendor, long>();
        vendorRepo.Setup(r => r.GetAsync(VendorId)).ReturnsAsync(new Vendor { Id = VendorId, UserId = VendorUserId, BusinessName = "V" });

        var payment = CreateAuthorizedPayment();
        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Payment>>())).ReturnsAsync(payment);

        var job = CreateJob();
        await job.RunAsync();

        Assert.Equal(BookingStatus.Expired, booking.Status);
        Assert.Equal(AvailabilityStatus.Available, hold.Status);
        Assert.Null(hold.BookingRequestId);
        Assert.Null(hold.BookingRequest);
        Assert.Null(hold.HoldExpiresAt);
        slotRepo.Verify(r => r.Update(hold), Times.Once);
        slotRepo.Verify(r => r.Delete(It.IsAny<VendorAvailability>()), Times.Never);

        Assert.NotNull(capturedHistory);
        Assert.Equal("Pending", capturedHistory!.PreviousStatus);
        Assert.Equal("Expired", capturedHistory.NewStatus);
        Assert.Null(capturedHistory.ChangedByUserId);
        Assert.Equal("auto-expired: vendor did not respond within TTL", capturedHistory.Notes);

        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);

        _paymentGatewayServiceMock.Verify(g => g.CancelPaymentIntentAsync(It.Is<CancelPaymentIntentRequest>(
            r => r.PaymentIntentId == "pi_expired_hold")), Times.Once);
        Assert.Equal(PaymentStatus.Cancelled, payment.Status);
        Assert.NotNull(payment.CancelledAt);

        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            ClientUserId, "booking_request_expired", It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            VendorUserId, "booking_request_expired", It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_VoidFails_StillExpiresBookingAndNotifiesBothParties()
    {
        var booking = CreateBooking();
        var hold = CreateExpiredHold(booking);

        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<VendorAvailability> { hold });

        _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();

        var clientRepo = _unitOfWorkMock.SetupRepository<Client, long>();
        clientRepo.Setup(r => r.GetAsync(ClientId)).ReturnsAsync(new Client { Id = ClientId, UserId = ClientUserId });

        var vendorRepo = _unitOfWorkMock.SetupRepository<Vendor, long>();
        vendorRepo.Setup(r => r.GetAsync(VendorId)).ReturnsAsync(new Vendor { Id = VendorId, UserId = VendorUserId, BusinessName = "V" });

        var payment = CreateAuthorizedPayment();
        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Payment>>())).ReturnsAsync(payment);

        _paymentGatewayServiceMock
            .Setup(g => g.CancelPaymentIntentAsync(It.IsAny<CancelPaymentIntentRequest>()))
            .ThrowsAsync(new InvalidOperationException("stripe unreachable"));

        var job = CreateJob();
        await job.RunAsync();

        Assert.Equal(BookingStatus.Expired, booking.Status);
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
        Assert.Equal(AvailabilityStatus.Available, hold.Status); // hold reset still happens regardless of void outcome

        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            ClientUserId, "booking_request_expired", It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            VendorUserId, "booking_request_expired", It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
    }
}
