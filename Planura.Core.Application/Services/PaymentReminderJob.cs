using Microsoft.Extensions.Options;
using Planura.Core.Application.Common;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Services;

public class PaymentReminderJob : IPaymentReminderJob
{
    private const int ReminderWindowHoursBeforeDeadline = 12;

    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly BookingOptions _bookingOptions;

    public PaymentReminderJob(
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IOptions<BookingOptions> bookingOptions)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _bookingOptions = bookingOptions.Value;
    }

    public async Task RunAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var reminderCutoff = now.AddHours(-(_bookingOptions.PaymentDeadlineHours - ReminderWindowHoursBeforeDeadline));
        var deadlineCutoff = now.AddHours(-_bookingOptions.PaymentDeadlineHours);

        var bookingRepo = _unitOfWork.Repository<BookingRequest, long>();
        var dueBookings = await bookingRepo.GetAllWithSpecAsync(
            new PaymentReminderDueBookingRequestsSpecification(reminderCutoff, deadlineCutoff));

        foreach (var booking in dueBookings)
        {
            booking.PaymentReminderSentAt = now;
            bookingRepo.Update(booking);

            var client = await _unitOfWork.Repository<Client, long>().GetAsync(booking.ClientId);
            if (client is not null)
            {
                await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
                    client.UserId,
                    NotificationTypes.PaymentDeadlineApproaching,
                    "Payment deadline approaching",
                    $"You have about {ReminderWindowHoursBeforeDeadline} hours left to pay for your booking on {booking.EventDate:d} before it is automatically cancelled."));
            }
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private static async Task NotifyBestEffortAsync(Func<Task> notify)
    {
        try
        {
            await notify();
        }
        catch
        {
            // Notifications are best-effort and must never fail the job.
        }
    }
}
