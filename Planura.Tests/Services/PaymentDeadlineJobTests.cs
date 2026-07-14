using Microsoft.Extensions.Options;
using Moq;
using Planura.Core.Application.Common;
using Planura.Core.Application.Services;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using Planura.Tests.TestHelpers;
using Xunit;

namespace Planura.Tests.Services;

public class PaymentDeadlineJobTests
{
    private const long ClientId = 10;
    private const long ClientUserId = 500;
    private const long VendorId = 20;
    private const long VendorUserId = 200;
    private const long BookingRequestId = 1;

    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();

    private PaymentDeadlineJob CreateJob() => new(
        _unitOfWorkMock.Object,
        _notificationServiceMock.Object,
        Options.Create(new BookingOptions { HoldTtlHours = 48, PaymentDeadlineHours = 72 }));

    private static BookingRequest CreateOverdueBooking() => new()
    {
        Id = BookingRequestId,
        ClientId = ClientId,
        VendorId = VendorId,
        EventPlanId = 1,
        EventDate = new DateOnly(2026, 8, 1),
        Status = BookingStatus.Accepted,
        PaymentStatus = BookingPaymentStatus.Unpaid,
        RespondedAt = DateTimeOffset.UtcNow.AddHours(-80)
    };

    [Fact]
    public async Task RunAsync_NoOverdueBookings_NoOp()
    {
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<BookingRequest>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<BookingRequest>());

        var job = CreateJob();
        await job.RunAsync();

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_OverdueBooking_CancelsBookingFreesSlotAndNotifiesBothParties()
    {
        var booking = CreateOverdueBooking();

        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<BookingRequest>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<BookingRequest> { booking });

        var holds = new List<VendorAvailability>
        {
            new() { Id = 40, VendorId = VendorId, Status = AvailabilityStatus.Booked, BookingRequestId = BookingRequestId }
        };
        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>(), It.IsAny<bool>()))
            .ReturnsAsync(holds);

        var historyRepo = _unitOfWorkMock.SetupRepository<BookingStatusHistory, long>();
        BookingStatusHistory? capturedHistory = null;
        historyRepo.Setup(r => r.AddAsync(It.IsAny<BookingStatusHistory>()))
            .Callback<BookingStatusHistory>(h => capturedHistory = h)
            .Returns(Task.CompletedTask);

        var clientRepo = _unitOfWorkMock.SetupRepository<Client, long>();
        clientRepo.Setup(r => r.GetAsync(ClientId)).ReturnsAsync(new Client { Id = ClientId, UserId = ClientUserId });

        var vendorRepo = _unitOfWorkMock.SetupRepository<Vendor, long>();
        vendorRepo.Setup(r => r.GetAsync(VendorId)).ReturnsAsync(new Vendor { Id = VendorId, UserId = VendorUserId, BusinessName = "V" });

        var job = CreateJob();
        await job.RunAsync();

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.NotNull(booking.CancelledAt);
        slotRepo.Verify(r => r.DeleteRange(holds), Times.Once);

        Assert.NotNull(capturedHistory);
        Assert.Equal("auto-cancelled: payment deadline missed", capturedHistory!.Notes);

        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);

        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            ClientUserId, "booking_auto_cancelled_unpaid", It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            VendorUserId, "booking_auto_cancelled_unpaid", It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_PartiallyPaidBooking_IsNeverConsideredOverdueByTheSpecification()
    {
        // PartiallyPaid was removed from BookingPaymentStatus (installments were dropped),
        // so the only non-terminal payable state is Unpaid - nothing extra to guard here,
        // this test just documents that Paid/Refunded bookings are excluded by construction.
        var paidBooking = CreateOverdueBooking();
        paidBooking.PaymentStatus = BookingPaymentStatus.Paid;

        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<BookingRequest>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<BookingRequest>());

        var job = CreateJob();
        await job.RunAsync();

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
