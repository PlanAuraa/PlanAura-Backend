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

public class PaymentReminderJobTests
{
    private const long ClientId = 10;
    private const long ClientUserId = 500;
    private const long BookingRequestId = 1;

    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();

    private PaymentReminderJob CreateJob() => new(
        _unitOfWorkMock.Object,
        _notificationServiceMock.Object,
        Options.Create(new BookingOptions { HoldTtlHours = 48, PaymentDeadlineHours = 72 }));

    private static BookingRequest CreateDueBooking() => new()
    {
        Id = BookingRequestId,
        ClientId = ClientId,
        VendorId = 20,
        EventPlanId = 1,
        EventDate = new DateOnly(2026, 8, 1),
        Status = BookingStatus.Accepted,
        PaymentStatus = BookingPaymentStatus.Unpaid,
        RespondedAt = DateTimeOffset.UtcNow.AddHours(-61),
        PaymentReminderSentAt = null
    };

    [Fact]
    public async Task RunAsync_NoDueBookings_SavesButNotifiesNoOne()
    {
        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<BookingRequest>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<BookingRequest>());

        var job = CreateJob();
        await job.RunAsync();

        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_DueBooking_MarksReminderSentAndNotifiesClient()
    {
        var booking = CreateDueBooking();

        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<BookingRequest>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<BookingRequest> { booking });

        var clientRepo = _unitOfWorkMock.SetupRepository<Client, long>();
        clientRepo.Setup(r => r.GetAsync(ClientId)).ReturnsAsync(new Client { Id = ClientId, UserId = ClientUserId });

        var job = CreateJob();
        await job.RunAsync();

        Assert.NotNull(booking.PaymentReminderSentAt);
        bookingRepo.Verify(r => r.Update(booking), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        _notificationServiceMock.Verify(n => n.NotifyUserAsync(
            ClientUserId, "payment_deadline_approaching", It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
    }
}
