using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Planura.Core.Application.Common;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Services.RemainderGraceExpiryJob;

/// <summary>
/// Two time-driven passes for the deposit remainder grace period (Phase 3):
///   1. Grace expired: a deposit booking whose remainder charge failed and whose grace window
///      (RemainderFailedAt + GracePeriodDays) has elapsed, still unpaid, is routed to the EXISTING admin
///      cancellation-review queue (Status → CancellationRequested, RefundStatus → PendingReview, refund
///      estimate 0 — the deposit is forfeited by policy). The slot is released by the admin on approval, not
///      here. A client paying at the last moment moves the payment out of RemainderFailed, so it is not
///      selected — no race.
///   2. Stuck claim recovery: a payment left in the transient RemainderCharging state past the timeout
///      (e.g. an abandoned on-session SCA) is atomically reclaimed to RemainderFailed so it is never stuck.
/// </summary>
public class RemainderGraceExpiryJob : IRemainderGraceExpiryJob
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly BookingOptions _bookingOptions;
    private readonly ILogger<RemainderGraceExpiryJob> _logger;

    public RemainderGraceExpiryJob(
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IOptions<BookingOptions> bookingOptions,
        ILogger<RemainderGraceExpiryJob> logger)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _bookingOptions = bookingOptions.Value;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        var now = DateTimeOffset.UtcNow;

        // Pass 2 first: reclaim stuck claims so an abandoned SCA that has since passed its grace can be picked
        // up by pass 1 in the same run.
        await ReclaimStuckChargingAsync(now);
        await RouteGraceExpiredToReviewAsync(now);
    }

    private async Task RouteGraceExpiredToReviewAsync(DateTimeOffset now)
    {
        var graceCutoff = now.AddDays(-_bookingOptions.GracePeriodDays);
        var expiredPayments = await _unitOfWork.Repository<Payment, long>()
            .GetAllWithSpecAsync(new GraceExpiredRemainderFailedPaymentsSpecification(graceCutoff));

        foreach (var payment in expiredPayments)
        {
            var booking = await _unitOfWork.Repository<BookingRequest, long>().GetAsync(payment.BookingRequestId);

            // Only route a still-active booking; if it was already cancelled/requested/paid, skip it. This also
            // covers a client who just paid (payment would no longer be RemainderFailed, but guard anyway).
            if (booking is null
                || booking.Status != BookingStatus.Accepted
                || booking.PaymentStatus != BookingPaymentStatus.RemainderFailed)
            {
                continue;
            }

            await RouteBookingToReviewAsync(booking, now);
        }
    }

    private async Task RouteBookingToReviewAsync(BookingRequest booking, DateTimeOffset now)
    {
        // Mirrors BookingService.RequestCancellationAsync's shape so it lands in the same admin queue. Refund
        // estimate is 0 — the deposit is forfeited by policy; the admin approves (cancel + release slot, no
        // refund) or rejects (give the client more time).
        booking.Status = BookingStatus.CancellationRequested;
        booking.CancellationReason = "Automatically flagged: the remaining balance was not paid and the grace period elapsed.";
        booking.CancellationRequestedAt = now;
        booking.CancellationRefundPercent = 0m;
        booking.CancellationRefundAmount = 0m;
        booking.RefundStatus = RefundStatus.PendingReview;
        booking.UpdatedAt = now;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            _unitOfWork.Repository<BookingRequest, long>().Update(booking);

            var history = new BookingStatusHistory
            {
                BookingRequestId = booking.Id,
                PreviousStatus = BookingStatus.Accepted.ToString(),
                NewStatus = BookingStatus.CancellationRequested.ToString(),
                ChangedByUserId = null,
                Notes = "auto: remaining balance unpaid and grace period elapsed — routed to admin cancellation review (deposit forfeited)"
            };
            await _unitOfWork.Repository<BookingStatusHistory, long>().AddAsync(history);

            await _unitOfWork.CommitTransactionAsync();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex,
                "Failed to route grace-expired booking {BookingId} to admin cancellation review.", booking.Id);
            return;
        }

        await NotifyBestEffortAsync(() => _notificationService.NotifyRoleAsync(
            Roles.Admin,
            NotificationTypes.BookingCancellationRequested,
            "A booking needs cancellation review (unpaid remainder)",
            $"The booking for {booking.EventDate:d} had its remaining balance unpaid past the grace period and " +
            "was flagged for cancellation review (deposit forfeited). Please review."));

        var client = await _unitOfWork.Repository<Client, long>().GetAsync(booking.ClientId);
        if (client is not null)
        {
            await NotifyBestEffortAsync(() => _notificationService.NotifyUserWithEmailAsync(
                client.UserId,
                NotificationTypes.RemainderFailed,
                "Your booking is being reviewed for cancellation",
                $"The remaining balance for your booking on {booking.EventDate:d} was not paid in time, so it has been " +
                "flagged for cancellation review.",
                emailSubject: "Your booking is being reviewed for cancellation",
                emailBody: $"The remaining balance for your booking on {booking.EventDate:d} was not paid within the grace " +
                           "period, so it has been flagged for cancellation review by our team."));
        }
    }

    private async Task ReclaimStuckChargingAsync(DateTimeOffset now)
    {
        var chargingCutoff = now.AddMinutes(-_bookingOptions.RemainderChargingTimeoutMinutes);
        var stuckPayments = await _unitOfWork.Repository<Payment, long>()
            .GetAllWithSpecAsync(new StuckRemainderChargingPaymentsSpecification(chargingCutoff));

        foreach (var payment in stuckPayments)
        {
            // Atomic reclaim: only succeeds if still RemainderCharging (can't clobber a payment a webhook just
            // finalized). The remainder-charge job / client pay-remainder can then act on it again.
            var reclaimed = await _unitOfWork.TryReclaimStuckRemainderChargeAsync(
                payment.Id, "Reclaimed: the payment attempt was not completed in time.");
            if (!reclaimed)
            {
                continue;
            }

            var booking = await _unitOfWork.Repository<BookingRequest, long>().GetAsync(payment.BookingRequestId);
            if (booking is not null && booking.PaymentStatus != BookingPaymentStatus.RemainderFailed)
            {
                booking.PaymentStatus = BookingPaymentStatus.RemainderFailed;
                booking.UpdatedAt = now;
                _unitOfWork.Repository<BookingRequest, long>().Update(booking);
                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation(
                "Reclaimed stuck RemainderCharging payment {PaymentId} (booking {BookingId}) back to RemainderFailed.",
                payment.Id, payment.BookingRequestId);
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
            _logger.LogWarning(ex, "Failed to send a remainder grace-expiry notification.");
        }
    }
}
