using Moq;
using Planura.Core.Application.Services;
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

    private BookingHoldExpiryJob CreateJob() => new(_unitOfWorkMock.Object, _notificationServiceMock.Object);

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
    public async Task RunAsync_ExpiredHoldWithPendingBooking_ExpiresBookingDeletesHoldAndNotifiesBothParties()
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

        var job = CreateJob();
        await job.RunAsync();

        Assert.Equal(BookingStatus.Expired, booking.Status);
        slotRepo.Verify(r => r.Delete(hold), Times.Once);

        Assert.NotNull(capturedHistory);
        Assert.Equal("Pending", capturedHistory!.PreviousStatus);
        Assert.Equal("Expired", capturedHistory.NewStatus);
        Assert.Null(capturedHistory.ChangedByUserId);
        Assert.Equal("auto-expired: vendor did not respond within TTL", capturedHistory.Notes);

        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);

        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            ClientUserId, "booking_request_expired", It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            VendorUserId, "booking_request_expired", It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
    }
}
