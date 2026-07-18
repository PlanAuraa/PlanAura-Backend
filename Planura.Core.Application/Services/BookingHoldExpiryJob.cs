using Microsoft.Extensions.Logging;
using Planura.Core.Application.Abstraction.PaymentGateway;
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
    private readonly IPaymentGatewayService _paymentGatewayService;
    private readonly ILogger<BookingHoldExpiryJob> _logger;

    public BookingHoldExpiryJob(
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IPaymentGatewayService paymentGatewayService,
        ILogger<BookingHoldExpiryJob> logger)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _paymentGatewayService = paymentGatewayService;
        _logger = logger;
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

                hold.Status = AvailabilityStatus.Available;
                hold.BookingRequestId = null;
                hold.BookingRequest = null;
                hold.HoldExpiresAt = null;
                _unitOfWork.Repository<VendorAvailability, long>().Update(hold);

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

            var payment = await _unitOfWork.Repository<Payment, long>()
                .GetWithSpecAsync(new AuthorizedPaymentByBookingRequestSpecification(booking.Id));
            if (payment is not null)
            {
                await VoidAuthorizationBestEffortAsync(payment);
            }

            await NotifyBothPartiesAsync(booking);
        }
    }

    /// <summary>
    /// Best-effort void, mirroring BookingService's: no money has moved on an un-captured authorization, so a
    /// failure here just means the hold releases naturally on Stripe's side instead of immediately.
    /// </summary>
    private async Task VoidAuthorizationBestEffortAsync(Payment payment)
    {
        try
        {
            await _paymentGatewayService.CancelPaymentIntentAsync(new CancelPaymentIntentRequest
            {
                PaymentIntentId = payment.GatewayReference!,
                IdempotencyKey = $"void-{payment.Id}"
            });

            payment.Status = PaymentStatus.Cancelled;
            payment.CancelledAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<Payment, long>().Update(payment);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to void Stripe authorization for PaymentIntent {PaymentIntentId} (hold expired, vendor " +
                "did not respond). The hold will still expire naturally on Stripe's side; Payment {PaymentId} " +
                "remains marked Authorized locally until reconciled.",
                payment.GatewayReference, payment.Id);
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
