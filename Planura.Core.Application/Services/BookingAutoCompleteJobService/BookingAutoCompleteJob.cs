using Microsoft.Extensions.Logging;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Services.BookingAutoCompleteJob;

public class BookingAutoCompleteJob : IBookingAutoCompleteJob
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly ILogger<BookingAutoCompleteJob> _logger;

    public BookingAutoCompleteJob(
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        ILogger<BookingAutoCompleteJob> logger)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        var now = DateTimeOffset.UtcNow;

        var toComplete = await _unitOfWork.Repository<BookingRequest, long>()
            .GetAllWithSpecAsync(new AcceptedPaidBookingsPastEndAtSpecification(now));

        foreach (var booking in toComplete)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                booking.Status = BookingStatus.Completed;
                booking.CompletedAt = now;
                booking.UpdatedAt = now;
                _unitOfWork.Repository<BookingRequest, long>().Update(booking);

                var history = new BookingStatusHistory
                {
                    BookingRequestId = booking.Id,
                    PreviousStatus = BookingStatus.Accepted.ToString(),
                    NewStatus = BookingStatus.Completed.ToString(),
                    ChangedByUserId = null,
                    Notes = "auto-completed: booked slot's end time has passed"
                };
                await _unitOfWork.Repository<BookingStatusHistory, long>().AddAsync(history);

                await _unitOfWork.CommitTransactionAsync();
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }

            await NotifyBothPartiesAsync(booking);
        }
    }

    private async Task NotifyBothPartiesAsync(BookingRequest booking)
    {
        var client = await _unitOfWork.Repository<Client, long>().GetAsync(booking.ClientId);
        if (client is not null)
        {
            await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
                client.UserId,
                NotificationTypes.BookingCompleted,
                "Booking completed",
                $"Your booking for {booking.EventDate:d} is now marked complete."));
        }

        var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(booking.VendorId);
        if (vendor is not null)
        {
            await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
                vendor.UserId,
                NotificationTypes.BookingCompleted,
                "Booking completed",
                $"A booking for {booking.EventDate:d} is now marked complete."));
        }
    }

    private async Task NotifyBestEffortAsync(Func<Task> notify)
    {
        try
        {
            await notify();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send booking-completed notification.");
        }
    }
}
