using Microsoft.Extensions.Options;
using Planura.Core.Application.Common;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Services;

public class PaymentDeadlineJob : IPaymentDeadlineJob
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly BookingOptions _bookingOptions;

    public PaymentDeadlineJob(
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
        var cutoff = now.AddHours(-_bookingOptions.PaymentDeadlineHours);

        var bookingRepo = _unitOfWork.Repository<BookingRequest, long>();
        var overdueBookings = await bookingRepo.GetAllWithSpecAsync(
            new UnpaidPastDeadlineBookingRequestsSpecification(cutoff));

        foreach (var booking in overdueBookings)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                booking.Status = BookingStatus.Cancelled;
                booking.CancelledAt = now;
                booking.UpdatedAt = now;
                bookingRepo.Update(booking);

                var slotRepo = _unitOfWork.Repository<VendorAvailability, long>();
                var holds = await slotRepo.GetAllWithSpecAsync(
                    new VendorAvailabilityByBookingRequestSpecification(booking.Id));
                slotRepo.DeleteRange(holds);

                var history = new BookingStatusHistory
                {
                    BookingRequestId = booking.Id,
                    PreviousStatus = BookingStatus.Accepted.ToString(),
                    NewStatus = BookingStatus.Cancelled.ToString(),
                    ChangedByUserId = null,
                    Notes = "auto-cancelled: payment deadline missed"
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
                NotificationTypes.BookingAutoCancelledUnpaid,
                "Booking cancelled - payment deadline missed",
                $"Your booking for {booking.EventDate:d} was cancelled because payment was not completed in time."));
        }

        var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(booking.VendorId);
        if (vendor is not null)
        {
            await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
                vendor.UserId,
                NotificationTypes.BookingAutoCancelledUnpaid,
                "Booking cancelled - payment deadline missed",
                $"The booking for {booking.EventDate:d} was cancelled because the client did not complete payment in time."));
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
