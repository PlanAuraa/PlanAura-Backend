using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Planura.Core.Application.Abstraction.PaymentGateway;
using Planura.Core.Application.Common;
using Planura.Core.Application.Services;
using Planura.Core.Application.Services.RemainderChargeJob;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;
using Planura.Tests.TestHelpers;
using Xunit;

namespace Planura.Tests.Services;

public class RemainderChargeJobTests
{
    private const long ClientId = 10;
    private const long ClientUserId = 500;
    private const long VendorId = 20;
    private const long BookingRequestId = 60;
    private const long PaymentId = 7;

    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IPaymentGatewayService> _paymentGatewayServiceMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();

    private RemainderChargeJob CreateJob() => new(
        _unitOfWorkMock.Object,
        _paymentGatewayServiceMock.Object,
        _notificationServiceMock.Object,
        Options.Create(new BookingOptions { RemainderChargeLeadDays = 4 }),
        Options.Create(new StripeOptions { DefaultCurrency = "EGP" }),
        NullLogger<RemainderChargeJob>.Instance);

    private static BookingRequest CreateDepositBooking() => new()
    {
        Id = BookingRequestId,
        ClientId = ClientId,
        VendorId = VendorId,
        EventPlanId = 1,
        EventDate = DateOnly.FromDateTime(DateTimeOffset.UtcNow.AddDays(3).UtcDateTime),
        Status = BookingStatus.Accepted,
        PaymentStatus = BookingPaymentStatus.DepositPaid
    };

    private static Payment CreateRemainderDuePayment(
        string? savedPaymentMethodId = "pm_saved") => new()
    {
        Id = PaymentId,
        BookingRequestId = BookingRequestId,
        ClientId = ClientId,
        VendorId = VendorId,
        Amount = 1000m,
        IsDeposit = true,
        DepositAmount = 1000m,
        TotalAmount = 5000m,
        Status = PaymentStatus.DepositPaid_RemainderDue,
        GatewayReference = "pi_deposit",
        SavedPaymentMethodId = savedPaymentMethodId
    };

    private void SetupRepos(BookingRequest booking, Payment? payment, Client? client)
    {
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<BookingRequest>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<BookingRequest> { booking });

        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Payment>>())).ReturnsAsync(payment);

        var clientRepo = _unitOfWorkMock.SetupRepository<Client, long>();
        clientRepo.Setup(r => r.GetAsync(ClientId)).ReturnsAsync(client);

        _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();

        // By default the job wins the atomic claim (DepositPaid_RemainderDue -> RemainderCharging).
        _unitOfWorkMock
            .Setup(u => u.TryClaimRemainderChargeAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task RunAsync_DepositRemainderDue_ChargesRemainderOffSessionAndMarksFullyPaid()
    {
        var booking = CreateDepositBooking();
        var payment = CreateRemainderDuePayment();
        var client = new Client { Id = ClientId, UserId = 500, StripeCustomerId = "cus_x" };
        SetupRepos(booking, payment, client);

        ChargeOffSessionRequest? chargeRequest = null;
        _paymentGatewayServiceMock
            .Setup(g => g.ChargeOffSessionAsync(It.IsAny<ChargeOffSessionRequest>()))
            .Callback<ChargeOffSessionRequest>(r => chargeRequest = r)
            .ReturnsAsync(new PaymentIntentResult { PaymentIntentId = "pi_remainder", ClientSecret = "s", Status = "succeeded" });

        await CreateJob().RunAsync();

        // Remainder = TotalAmount(5000) - DepositAmount(1000) = 4000.00 => 400000 smallest unit.
        Assert.NotNull(chargeRequest);
        Assert.Equal(400000, chargeRequest!.AmountInSmallestUnit);
        Assert.Equal("cus_x", chargeRequest.CustomerId);
        Assert.Equal("pm_saved", chargeRequest.PaymentMethodId);
        Assert.Equal($"remainder-{PaymentId}", chargeRequest.IdempotencyKey); // stable per-payment key

        Assert.Equal(PaymentStatus.FullyPaid, payment.Status);
        Assert.Equal("pi_remainder", payment.RemainderGatewayReference);
        Assert.NotNull(payment.RemainderChargedAt);
        Assert.Equal(BookingPaymentStatus.Paid, booking.PaymentStatus);

        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Client is notified (in-app + email) that the booking is fully paid.
        _notificationServiceMock.Verify(n => n.NotifyUserWithEmailAsync(
            ClientUserId, NotificationTypes.RemainderPaid, It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_RunTwice_ChargesRemainderOnlyOnce()
    {
        var booking = CreateDepositBooking();
        var payment = CreateRemainderDuePayment();
        var client = new Client { Id = ClientId, UserId = 500, StripeCustomerId = "cus_x" };
        SetupRepos(booking, payment, client);

        _paymentGatewayServiceMock
            .Setup(g => g.ChargeOffSessionAsync(It.IsAny<ChargeOffSessionRequest>()))
            .ReturnsAsync(new PaymentIntentResult { PaymentIntentId = "pi_remainder", ClientSecret = "s", Status = "succeeded" });

        var job = CreateJob();
        await job.RunAsync(); // charges, payment -> FullyPaid
        await job.RunAsync(); // state gate now skips it

        // The state gate (payment no longer DepositPaid_RemainderDue after run 1) prevents a second charge.
        _paymentGatewayServiceMock.Verify(g => g.ChargeOffSessionAsync(It.IsAny<ChargeOffSessionRequest>()), Times.Once);
        Assert.Equal(PaymentStatus.FullyPaid, payment.Status);
    }

    [Fact]
    public async Task RunAsync_NoSavedCard_SkipsWithoutCharging()
    {
        // Pre-Phase-2 deposit booking: no saved payment method -> must be skipped, never charged.
        var booking = CreateDepositBooking();
        var payment = CreateRemainderDuePayment(savedPaymentMethodId: null);
        var client = new Client { Id = ClientId, UserId = 500, StripeCustomerId = "cus_x" };
        SetupRepos(booking, payment, client);

        await CreateJob().RunAsync();

        _paymentGatewayServiceMock.Verify(g => g.ChargeOffSessionAsync(It.IsAny<ChargeOffSessionRequest>()), Times.Never);
        Assert.Equal(PaymentStatus.DepositPaid_RemainderDue, payment.Status); // untouched
    }

    [Fact]
    public async Task RunAsync_PaymentAlreadyFullyPaid_SkipsWithoutCharging()
    {
        // Defensive: if the selected payment is no longer in the chargeable state, the gate skips it.
        var booking = CreateDepositBooking();
        var payment = CreateRemainderDuePayment();
        payment.Status = PaymentStatus.FullyPaid;
        var client = new Client { Id = ClientId, UserId = 500, StripeCustomerId = "cus_x" };
        SetupRepos(booking, payment, client);

        await CreateJob().RunAsync();

        _paymentGatewayServiceMock.Verify(g => g.ChargeOffSessionAsync(It.IsAny<ChargeOffSessionRequest>()), Times.Never);
    }

    // ---------------- Failure -> RemainderFailed (Phase 2, Section C) ----------------

    [Fact]
    public async Task RunAsync_ChargeFails_TransitionsToRemainderFailedWithReason()
    {
        var booking = CreateDepositBooking();
        var payment = CreateRemainderDuePayment();
        var client = new Client { Id = ClientId, UserId = 500, StripeCustomerId = "cus_x" };
        SetupRepos(booking, payment, client);

        _paymentGatewayServiceMock
            .Setup(g => g.ChargeOffSessionAsync(It.IsAny<ChargeOffSessionRequest>()))
            .ThrowsAsync(new PaymentDeclinedExeption("Your card has insufficient funds."));

        await CreateJob().RunAsync();

        Assert.Equal(PaymentStatus.RemainderFailed, payment.Status);
        Assert.Contains("insufficient funds", payment.RemainderFailureReason);
        Assert.NotNull(payment.RemainderFailedAt); // grace clock started
        Assert.Equal(BookingPaymentStatus.RemainderFailed, booking.PaymentStatus);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Client is notified (in-app + email) to pay the remainder.
        _notificationServiceMock.Verify(n => n.NotifyUserWithEmailAsync(
            ClientUserId, NotificationTypes.RemainderFailed, It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_AfterFailure_DoesNotRetryTheCharge()
    {
        // Once in RemainderFailed the payment gate excludes it — no hourly charge-spam.
        var booking = CreateDepositBooking();
        var payment = CreateRemainderDuePayment();
        var client = new Client { Id = ClientId, UserId = 500, StripeCustomerId = "cus_x" };
        SetupRepos(booking, payment, client);

        _paymentGatewayServiceMock
            .Setup(g => g.ChargeOffSessionAsync(It.IsAny<ChargeOffSessionRequest>()))
            .ThrowsAsync(new PaymentDeclinedExeption("Your card was declined."));

        var job = CreateJob();
        await job.RunAsync(); // fails -> RemainderFailed
        await job.RunAsync(); // gate skips it now

        _paymentGatewayServiceMock.Verify(g => g.ChargeOffSessionAsync(It.IsAny<ChargeOffSessionRequest>()), Times.Once);
        Assert.Equal(PaymentStatus.RemainderFailed, payment.Status);
    }

    [Fact]
    public async Task RunAsync_CannotClaim_SkipsWithoutCharging()
    {
        // Interleaving: the client's on-session pay-remainder already claimed this payment, so the job's
        // atomic claim fails -> it must NOT charge (no double-charge).
        var booking = CreateDepositBooking();
        var payment = CreateRemainderDuePayment();
        var client = new Client { Id = ClientId, UserId = ClientUserId, StripeCustomerId = "cus_x" };
        SetupRepos(booking, payment, client);
        _unitOfWorkMock
            .Setup(u => u.TryClaimRemainderChargeAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // client (or another run) holds the claim

        await CreateJob().RunAsync();

        _paymentGatewayServiceMock.Verify(g => g.ChargeOffSessionAsync(It.IsAny<ChargeOffSessionRequest>()), Times.Never);
    }

    // ---- Spec-level filtering: full-path / not-due / non-accepted bookings must not be selected ----

    [Fact]
    public void RemainderDueSpec_SelectsOnlyAcceptedDepositPaidBookingsWithinLead()
    {
        var now = DateTimeOffset.UtcNow;
        var leadCutoff = now.AddDays(4);
        var predicate = new RemainderDueBookingsWithinLeadSpecification(leadCutoff).Criteria!.Compile();

        BookingRequest Make(BookingStatus status, BookingPaymentStatus payStatus, DateTimeOffset slotStart) => new()
        {
            Status = status,
            PaymentStatus = payStatus,
            VendorAvailability = new List<VendorAvailability>
            {
                new() { StartAt = slotStart, EndAt = slotStart.AddHours(4) }
            }
        };

        var due = Make(BookingStatus.Accepted, BookingPaymentStatus.DepositPaid, now.AddDays(3));
        var fullPath = Make(BookingStatus.Accepted, BookingPaymentStatus.Paid, now.AddDays(3));
        var notDue = Make(BookingStatus.Accepted, BookingPaymentStatus.DepositPaid, now.AddDays(30));
        var notAccepted = Make(BookingStatus.Pending, BookingPaymentStatus.DepositPaid, now.AddDays(3));
        var alreadyFailed = Make(BookingStatus.Accepted, BookingPaymentStatus.RemainderFailed, now.AddDays(3));

        Assert.True(predicate(due));
        Assert.False(predicate(fullPath));    // full-payment booking excluded
        Assert.False(predicate(notDue));      // event still beyond the lead window
        Assert.False(predicate(notAccepted)); // not yet accepted
        Assert.False(predicate(alreadyFailed)); // RemainderFailed excluded -> no retry-spam
    }
}
