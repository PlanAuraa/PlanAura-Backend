using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Planura.Core.Application.Common;
using Planura.Core.Application.Services;
using Planura.Core.Application.Services.RemainderGraceExpiryJob;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using Planura.Tests.TestHelpers;
using Xunit;

namespace Planura.Tests.Services;

public class RemainderGraceExpiryJobTests
{
    private const long ClientId = 10;
    private const long ClientUserId = 500;
    private const long VendorId = 20;
    private const long BookingRequestId = 60;
    private const long PaymentId = 7;

    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();

    private RemainderGraceExpiryJob CreateJob() => new(
        _unitOfWorkMock.Object,
        _notificationServiceMock.Object,
        Options.Create(new BookingOptions { GracePeriodDays = 2, RemainderChargingTimeoutMinutes = 60 }),
        NullLogger<RemainderGraceExpiryJob>.Instance);

    private Mock<IGenericRepository<Payment, long>> SetupPaymentSpecs(
        IEnumerable<Payment>? graceExpired = null, IEnumerable<Payment>? stuckCharging = null)
    {
        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetAllWithSpecAsync(
                It.Is<ISpecification<Payment>>(s => s is GraceExpiredRemainderFailedPaymentsSpecification), It.IsAny<bool>()))
            .ReturnsAsync(graceExpired ?? new List<Payment>());
        paymentRepo.Setup(r => r.GetAllWithSpecAsync(
                It.Is<ISpecification<Payment>>(s => s is StuckRemainderChargingPaymentsSpecification), It.IsAny<bool>()))
            .ReturnsAsync(stuckCharging ?? new List<Payment>());
        return paymentRepo;
    }

    private static Payment CreateRemainderFailedPayment() => new()
    {
        Id = PaymentId,
        BookingRequestId = BookingRequestId,
        ClientId = ClientId,
        VendorId = VendorId,
        IsDeposit = true,
        DepositAmount = 1000m,
        TotalAmount = 5000m,
        Status = PaymentStatus.RemainderFailed,
        RemainderFailedAt = DateTimeOffset.UtcNow.AddDays(-3) // grace (2d) elapsed
    };

    private Mock<IGenericRepository<BookingRequest, long>> SetupBookingRepo(BookingRequest booking)
    {
        var repo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        repo.Setup(r => r.GetAsync(BookingRequestId)).ReturnsAsync(booking);
        return repo;
    }

    private static BookingRequest CreateBooking(
        BookingStatus status = BookingStatus.Accepted,
        BookingPaymentStatus paymentStatus = BookingPaymentStatus.RemainderFailed) => new()
    {
        Id = BookingRequestId,
        ClientId = ClientId,
        VendorId = VendorId,
        EventPlanId = 1,
        EventDate = new DateOnly(2026, 8, 1),
        Status = status,
        PaymentStatus = paymentStatus
    };

    [Fact]
    public async Task RunAsync_GraceExpiredUnpaid_RoutesToAdminReviewWithZeroRefund()
    {
        SetupPaymentSpecs(graceExpired: new List<Payment> { CreateRemainderFailedPayment() });
        var booking = CreateBooking();
        SetupBookingRepo(booking);
        _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();
        var clientRepo = _unitOfWorkMock.SetupRepository<Client, long>();
        clientRepo.Setup(r => r.GetAsync(ClientId)).ReturnsAsync(new Client { Id = ClientId, UserId = ClientUserId });

        await CreateJob().RunAsync();

        // Routed to the EXISTING admin cancellation-review queue; deposit forfeited (0 refund); slot NOT
        // released here (admin approve does that).
        Assert.Equal(BookingStatus.CancellationRequested, booking.Status);
        Assert.Equal(RefundStatus.PendingReview, booking.RefundStatus);
        Assert.Equal(0m, booking.CancellationRefundAmount);
        Assert.NotNull(booking.CancellationRequestedAt);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notificationServiceMock.Verify(n => n.NotifyRoleAsync(
            Roles.Admin, NotificationTypes.BookingCancellationRequested, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_ClientPaidAtExpiry_DoesNotCancel()
    {
        // Race guard: the payment was selected as grace-expired, but the booking is no longer RemainderFailed
        // (the client paid) -> must NOT be cancelled.
        SetupPaymentSpecs(graceExpired: new List<Payment> { CreateRemainderFailedPayment() });
        var booking = CreateBooking(status: BookingStatus.Accepted, paymentStatus: BookingPaymentStatus.Paid);
        SetupBookingRepo(booking);

        await CreateJob().RunAsync();

        Assert.Equal(BookingStatus.Accepted, booking.Status); // untouched
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_BookingAlreadyCancellationRequested_Skips()
    {
        SetupPaymentSpecs(graceExpired: new List<Payment> { CreateRemainderFailedPayment() });
        var booking = CreateBooking(status: BookingStatus.CancellationRequested);
        SetupBookingRepo(booking);

        await CreateJob().RunAsync();

        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_BookingAlreadyCancelled_Skips()
    {
        // No double-handling: a booking the client already manually forfeit-cancelled (now Cancelled) must be
        // skipped by the grace-expiry job even though its payment is still grace-expired RemainderFailed.
        SetupPaymentSpecs(graceExpired: new List<Payment> { CreateRemainderFailedPayment() });
        var booking = CreateBooking(status: BookingStatus.Cancelled, paymentStatus: BookingPaymentStatus.RemainderFailed);
        SetupBookingRepo(booking);

        await CreateJob().RunAsync();

        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_StuckChargingPastTimeout_ReclaimedToRemainderFailed()
    {
        var stuck = new Payment
        {
            Id = PaymentId,
            BookingRequestId = BookingRequestId,
            ClientId = ClientId,
            VendorId = VendorId,
            Status = PaymentStatus.RemainderCharging,
            RemainderChargingSince = DateTimeOffset.UtcNow.AddHours(-2) // past the 60-min timeout
        };
        SetupPaymentSpecs(stuckCharging: new List<Payment> { stuck });
        var booking = CreateBooking(status: BookingStatus.Accepted, paymentStatus: BookingPaymentStatus.DepositPaid);
        SetupBookingRepo(booking);
        _unitOfWorkMock
            .Setup(u => u.TryReclaimStuckRemainderChargeAsync(PaymentId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateJob().RunAsync();

        _unitOfWorkMock.Verify(u => u.TryReclaimStuckRemainderChargeAsync(PaymentId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(BookingPaymentStatus.RemainderFailed, booking.PaymentStatus);
    }

    // ---- Spec-level date filtering ----

    [Fact]
    public void GraceExpiredSpec_SelectsOnlyRemainderFailedPastCutoff()
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddDays(-2);
        var predicate = new GraceExpiredRemainderFailedPaymentsSpecification(cutoff).Criteria!.Compile();

        var expired = new Payment { Status = PaymentStatus.RemainderFailed, RemainderFailedAt = now.AddDays(-3) };
        var withinGrace = new Payment { Status = PaymentStatus.RemainderFailed, RemainderFailedAt = now.AddHours(-1) };
        var wrongStatus = new Payment { Status = PaymentStatus.FullyPaid, RemainderFailedAt = now.AddDays(-3) };
        var noTimestamp = new Payment { Status = PaymentStatus.RemainderFailed, RemainderFailedAt = null };

        Assert.True(predicate(expired));
        Assert.False(predicate(withinGrace));  // grace not yet elapsed
        Assert.False(predicate(wrongStatus));  // not RemainderFailed
        Assert.False(predicate(noTimestamp));  // no failure timestamp
    }

    [Fact]
    public void StuckChargingSpec_SelectsOnlyRemainderChargingPastCutoff()
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddMinutes(-60);
        var predicate = new StuckRemainderChargingPaymentsSpecification(cutoff).Criteria!.Compile();

        var stuck = new Payment { Status = PaymentStatus.RemainderCharging, RemainderChargingSince = now.AddHours(-2) };
        var recent = new Payment { Status = PaymentStatus.RemainderCharging, RemainderChargingSince = now.AddMinutes(-5) };
        var notCharging = new Payment { Status = PaymentStatus.FullyPaid, RemainderChargingSince = now.AddHours(-2) };

        Assert.True(predicate(stuck));
        Assert.False(predicate(recent));      // within the timeout window
        Assert.False(predicate(notCharging)); // not RemainderCharging
    }
}
