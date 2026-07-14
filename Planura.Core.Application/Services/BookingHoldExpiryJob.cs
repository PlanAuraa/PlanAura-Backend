using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Services;

public class BookingHoldExpiryJob : IBookingHoldExpiryJob
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;

    public BookingHoldExpiryJob(IUnitOfWork unitOfWork, INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
    }

    public async Task RunAsync()
    {
        var now = DateTimeOffset.UtcNow;

        var expiredHolds = await _unitOfWork.Repository<VendorAvailability, long>()
            .GetAllWithSpecAsync(new HeldExpiredVendorAvailabilitySpecification(now));

        var toExpire = expiredHolds
            .Where(hold => hold.BookingRequest is not null && hold.BookingRequest.Status == BookingStatus.Pending)
            .ToList();

        foreach (var hold in toExpire)
        {
            var booking = hold.BookingRequest!;
            var previousStatus = booking.Status;

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                booking.Status = BookingStatus.Expired;
                booking.UpdatedAt = now;
                _unitOfWork.Repository<BookingRequest, long>().Update(booking);

                _unitOfWork.Repository<VendorAvailability, long>().Delete(hold);

                var history = new BookingStatusHistory
                {
                    BookingRequestId = booking.Id,
                    PreviousStatus = previousStatus.ToString(),
                    NewStatus = BookingStatus.Expired.ToString(),
                    ChangedByUserId = null,
                    Notes = "auto-expired: vendor did not respond within TTL"
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
                NotificationTypes.BookingRequestExpired,
                "Booking request expired",
                $"Your booking request for {booking.EventDate:d} expired because the vendor did not respond in time."));
        }

        var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(booking.VendorId);
        if (vendor is not null)
        {
            await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
                vendor.UserId,
                NotificationTypes.BookingRequestExpired,
                "Booking request expired",
                $"A booking request for {booking.EventDate:d} expired because it was not responded to in time."));
        }
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
