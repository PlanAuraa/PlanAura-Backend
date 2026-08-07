using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Planura.Core.Application.Common;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Services.BookingAutoCompleteJob;

/// <summary>
/// Two independent passes, both time-driven but never completing a booking on elapsed time alone:
///   1. Accepted+Paid bookings whose slot has ended move to AwaitingConfirmation and the client is
///      asked to confirm the service was delivered (see BookingService.ConfirmServiceDeliveredAsync
///      for the client-driven path, and RequestCancellationAsync/FlagDisputeAsync for the other
///      ways a booking can leave AwaitingConfirmation).
///   2. AwaitingConfirmation bookings whose grace window has elapsed with no open dispute
///      auto-confirm to Completed, so a silent client doesn't leave a booking stuck forever.
/// </summary>
public class BookingAutoCompleteJob : IBookingAutoCompleteJob
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly BookingOptions _bookingOptions;
    private readonly ILogger<BookingAutoCompleteJob> _logger;

    public BookingAutoCompleteJob(
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IOptions<BookingOptions> bookingOptions,
        ILogger<BookingAutoCompleteJob> logger)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _bookingOptions = bookingOptions.Value;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        var now = DateTimeOffset.UtcNow;

        await RunAwaitingConfirmationPassAsync(now);
        await RunAutoConfirmPassAsync(now);
    }

    /// <summary>Pass 1: Accepted+Paid, slot ended -> AwaitingConfirmation. Never -> Completed directly.</summary>
    private async Task RunAwaitingConfirmationPassAsync(DateTimeOffset now)
    {
        var toAwaitConfirmation = await _unitOfWork.Repository<BookingRequest, long>()
            .GetAllWithSpecAsync(new AcceptedPaidBookingsPastEndAtSpecification(now));

        foreach (var booking in toAwaitConfirmation)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                booking.Status = BookingStatus.AwaitingConfirmation;
                booking.AwaitingConfirmationSince = now;
                booking.UpdatedAt = now;
                _unitOfWork.Repository<BookingRequest, long>().Update(booking);

                var history = new BookingStatusHistory
                {
                    BookingRequestId = booking.Id,
                    PreviousStatus = BookingStatus.Accepted.ToString(),
                    NewStatus = BookingStatus.AwaitingConfirmation.ToString(),
                    ChangedByUserId = null,
                    Notes = "booked slot's end time has passed; awaiting client confirmation"
                };
                await _unitOfWork.Repository<BookingStatusHistory, long>().AddAsync(history);

                await _unitOfWork.CommitTransactionAsync();
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }

            var client = await _unitOfWork.Repository<Client, long>().GetAsync(booking.ClientId);
            if (client is not null)
            {
                await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
                    client.UserId,
                    NotificationTypes.BookingAwaitingConfirmation,
                    "Please confirm your booking",
                    $"Your event on {booking.EventDate:d} has passed — please confirm the service was delivered, " +
                    "or report a problem if something went wrong."));
            }
        }
    }

    /// <summary>Pass 2: AwaitingConfirmation past its grace window with no open dispute -> Completed.</summary>
    private async Task RunAutoConfirmPassAsync(DateTimeOffset now)
    {
        if (_bookingOptions.AutoConfirmAfterDays <= 0)
        {
            // Auto-confirm disabled: bookings wait indefinitely for an explicit client action.
            return;
        }

        var cutoff = now.AddDays(-_bookingOptions.AutoConfirmAfterDays);
        var toAutoConfirm = await _unitOfWork.Repository<BookingRequest, long>()
            .GetAllWithSpecAsync(new AwaitingConfirmationPastGraceSpecification(cutoff));

        foreach (var booking in toAutoConfirm)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                booking.Status = BookingStatus.Completed;
                booking.CompletedAt = now;
                booking.UpdatedAt = now;
                _unitOfWork.Repository<BookingRequest, long>().Update(booking);

                // Keep the vendor's denormalized completed-bookings counter in step with the status
                // change - it drives "events completed" on the public profile and dashboard. Both
                // the client-confirm path (BookingService.ConfirmServiceDeliveredAsync) and this
                // auto-confirm path are the only ways a booking reaches Completed, and each booking
                // completes exactly once, so a single increment here is exact.
                var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(booking.VendorId);
                if (vendor is not null)
                {
                    vendor.TotalCompletedBookings += 1;
                    vendor.UpdatedAt = now;
                    _unitOfWork.Repository<Vendor, long>().Update(vendor);
                }

                var history = new BookingStatusHistory
                {
                    BookingRequestId = booking.Id,
                    PreviousStatus = BookingStatus.AwaitingConfirmation.ToString(),
                    NewStatus = BookingStatus.Completed.ToString(),
                    ChangedByUserId = null,
                    Notes = $"auto-confirmed: client did not respond within {_bookingOptions.AutoConfirmAfterDays} day(s)"
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
            _logger.LogWarning(ex, "Failed to send booking auto-complete notification.");
        }
    }
}
