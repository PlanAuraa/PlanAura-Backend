using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Planura.Core.Application.Abstraction.PaymentGateway;
using Planura.Core.Application.Models.AdminPayment;
using Planura.Core.Application.Services.AdminPayment;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;
using Planura.Tests.TestHelpers;
using Xunit;

namespace Planura.Tests.Services;

public class AdminPaymentServiceTests
{
    private const long PaymentId = 1;
    private const long BookingRequestId = 60;
    private const long ClientId = 10;
    private const long VendorId = 20;
    private const long AdminId = 999;

    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IPaymentGatewayService> _paymentGatewayServiceMock = new();
    private readonly List<RefundPaymentIntentRequest> _refunds = new();

    private AdminPaymentService CreateService() => new(
        _unitOfWorkMock.Object,
        _paymentGatewayServiceMock.Object,
        NullLogger<AdminPaymentService>.Instance);

    private BookingRequest SetupPaymentAndBooking(Payment payment)
    {
        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo.Setup(r => r.GetAsync(PaymentId)).ReturnsAsync(payment);

        var booking = new BookingRequest
        {
            Id = BookingRequestId,
            ClientId = ClientId,
            VendorId = VendorId,
            EventPlanId = 1,
            EventDate = new DateOnly(2026, 8, 1),
            PaymentStatus = BookingPaymentStatus.Paid
        };
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAsync(BookingRequestId)).ReturnsAsync(booking);

        _paymentGatewayServiceMock
            .Setup(g => g.RefundPaymentIntentAsync(It.IsAny<RefundPaymentIntentRequest>()))
            .Callback<RefundPaymentIntentRequest>(r => _refunds.Add(r))
            .ReturnsAsync(new RefundResult { RefundId = "re_1", Status = "succeeded" });

        return booking;
    }

    private static Payment CreateFullPathPayment() => new()
    {
        Id = PaymentId,
        BookingRequestId = BookingRequestId,
        ClientId = ClientId,
        VendorId = VendorId,
        Amount = 5000m,
        Status = PaymentStatus.Completed,
        GatewayReference = "pi_full"
    };

    private static Payment CreateFullyPaidDepositPayment() => new()
    {
        Id = PaymentId,
        BookingRequestId = BookingRequestId,
        ClientId = ClientId,
        VendorId = VendorId,
        Amount = 1000m,               // amount held on the deposit PI
        IsDeposit = true,
        DepositAmount = 1000m,
        TotalAmount = 5000m,
        Status = PaymentStatus.FullyPaid,
        GatewayReference = "pi_deposit",
        RemainderGatewayReference = "pi_remainder"
    };

    [Fact]
    public async Task RefundPaymentAsync_FullPathCompleted_RefundsSinglePi_Unchanged()
    {
        var payment = CreateFullPathPayment();
        var booking = SetupPaymentAndBooking(payment);

        await CreateService().RefundPaymentAsync(PaymentId, AdminId, new RefundPaymentDto { Reason = "cancel" });

        var refund = Assert.Single(_refunds);
        Assert.Equal("pi_full", refund.PaymentIntentId);
        Assert.Null(refund.AmountInSmallestUnit); // full refund
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(BookingPaymentStatus.Refunded, booking.PaymentStatus);
    }

    [Fact]
    public async Task RefundPaymentAsync_FullyPaidDeposit_FullRefund_RefundsBothPisInFull()
    {
        var payment = CreateFullyPaidDepositPayment();
        var booking = SetupPaymentAndBooking(payment);

        await CreateService().RefundPaymentAsync(PaymentId, AdminId, new RefundPaymentDto { Reason = "cancel" });

        Assert.Equal(2, _refunds.Count);
        Assert.Contains(_refunds, r => r.PaymentIntentId == "pi_deposit" && r.AmountInSmallestUnit == null);
        Assert.Contains(_refunds, r => r.PaymentIntentId == "pi_remainder" && r.AmountInSmallestUnit == null);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(BookingPaymentStatus.Refunded, booking.PaymentStatus);
    }

    [Fact]
    public async Task RefundPaymentAsync_FullyPaidDeposit_PartialRefund_AllocatesDepositFirstThenRemainder()
    {
        var payment = CreateFullyPaidDepositPayment(); // deposit 1000, remainder 4000
        SetupPaymentAndBooking(payment);

        // 1500 => 1000 from the deposit PI, 500 from the remainder PI.
        await CreateService().RefundPaymentAsync(PaymentId, AdminId, new RefundPaymentDto { Reason = "partial", Amount = 1500m });

        Assert.Equal(2, _refunds.Count);
        Assert.Contains(_refunds, r => r.PaymentIntentId == "pi_deposit" && r.AmountInSmallestUnit == 100000);   // 1000.00
        Assert.Contains(_refunds, r => r.PaymentIntentId == "pi_remainder" && r.AmountInSmallestUnit == 50000);  // 500.00
    }

    [Fact]
    public async Task RefundPaymentAsync_FullyPaidDeposit_PartialWithinDeposit_RefundsOnlyDepositPi()
    {
        var payment = CreateFullyPaidDepositPayment();
        SetupPaymentAndBooking(payment);

        // 600 <= deposit(1000) => only the deposit PI is refunded; the remainder PI is untouched.
        await CreateService().RefundPaymentAsync(PaymentId, AdminId, new RefundPaymentDto { Reason = "partial", Amount = 600m });

        var refund = Assert.Single(_refunds);
        Assert.Equal("pi_deposit", refund.PaymentIntentId);
        Assert.Equal(60000, refund.AmountInSmallestUnit); // 600.00
    }

    [Fact]
    public async Task RefundPaymentAsync_DepositOnlyRemainderFailed_IsNonRefundable()
    {
        var payment = CreateFullyPaidDepositPayment();
        payment.Status = PaymentStatus.RemainderFailed; // only the deposit was captured
        SetupPaymentAndBooking(payment);

        await Assert.ThrowsAsync<BadRequestExeption>(() =>
            CreateService().RefundPaymentAsync(PaymentId, AdminId, new RefundPaymentDto { Reason = "cancel" }));

        _paymentGatewayServiceMock.Verify(g => g.RefundPaymentIntentAsync(It.IsAny<RefundPaymentIntentRequest>()), Times.Never);
    }

    [Fact]
    public async Task RefundPaymentAsync_DepositPaidRemainderDue_IsNonRefundable()
    {
        var payment = CreateFullyPaidDepositPayment();
        payment.Status = PaymentStatus.DepositPaid_RemainderDue;
        SetupPaymentAndBooking(payment);

        await Assert.ThrowsAsync<BadRequestExeption>(() =>
            CreateService().RefundPaymentAsync(PaymentId, AdminId, new RefundPaymentDto { Reason = "cancel" }));

        _paymentGatewayServiceMock.Verify(g => g.RefundPaymentIntentAsync(It.IsAny<RefundPaymentIntentRequest>()), Times.Never);
    }
}
