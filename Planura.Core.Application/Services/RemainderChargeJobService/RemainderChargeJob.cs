using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Planura.Core.Application.Abstraction.PaymentGateway;
using Planura.Core.Application.Common;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Services.RemainderChargeJob;

/// <summary>
/// Charges the outstanding remainder on deposit-path bookings off-session as the event approaches
/// (RemainderChargeLeadDays before StartAt). The remainder is derived server-side from the recorded split
/// (TotalAmount − DepositAmount) and charged against the card saved at booking time.
///
/// No double-charge — three layers:
///   1. Stripe idempotency key "remainder-{paymentId}" (stable per remainder) collapses any repeat or
///      overlapping call into a single charge at Stripe — the hard financial guarantee.
///   2. State gate: only a Payment in DepositPaid_RemainderDue with no RemainderGatewayReference is
///      charged. Success → FullyPaid, failure → RemainderFailed (Section C); once out of
///      DepositPaid_RemainderDue it is never selected again.
///   3. Compare-and-set: the success write re-checks the payment is still DepositPaid_RemainderDue inside
///      the transaction before recording, so a concurrent run can't double-write. The Hangfire recurring
///      job is also scheduled single-instance (DisableConcurrentExecution, in the API composition root).
/// </summary>
public class RemainderChargeJob : IRemainderChargeJob
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentGatewayService _paymentGatewayService;
    private readonly INotificationService _notificationService;
    private readonly BookingOptions _bookingOptions;
    private readonly StripeOptions _stripeOptions;
    private readonly ILogger<RemainderChargeJob> _logger;

    public RemainderChargeJob(
        IUnitOfWork unitOfWork,
        IPaymentGatewayService paymentGatewayService,
        INotificationService notificationService,
        IOptions<BookingOptions> bookingOptions,
        IOptions<StripeOptions> stripeOptions,
        ILogger<RemainderChargeJob> logger)
    {
        _unitOfWork = unitOfWork;
        _paymentGatewayService = paymentGatewayService;
        _notificationService = notificationService;
        _bookingOptions = bookingOptions.Value;
        _stripeOptions = stripeOptions.Value;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var leadCutoff = now.AddDays(_bookingOptions.RemainderChargeLeadDays);

        var dueBookings = await _unitOfWork.Repository<BookingRequest, long>()
            .GetAllWithSpecAsync(new RemainderDueBookingsWithinLeadSpecification(leadCutoff));

        foreach (var booking in dueBookings)
        {
            await TryChargeRemainderAsync(booking, now);
        }
    }

    private async Task TryChargeRemainderAsync(BookingRequest booking, DateTimeOffset now)
    {
        var payment = await _unitOfWork.Repository<Payment, long>()
            .GetWithSpecAsync(new RemainderChargeablePaymentByBookingSpecification(booking.Id));

        // State gate (layer 2): only a still-chargeable deposit payment that hasn't already been charged.
        if (payment is null
            || payment.Status != PaymentStatus.DepositPaid_RemainderDue
            || payment.RemainderGatewayReference is not null)
        {
            return;
        }

        // Skip (don't fail-loop) bookings with no saved card — e.g. deposit bookings created before Phase 2.
        var client = await _unitOfWork.Repository<Client, long>().GetAsync(booking.ClientId);
        if (string.IsNullOrWhiteSpace(payment.SavedPaymentMethodId) || string.IsNullOrWhiteSpace(client?.StripeCustomerId))
        {
            _logger.LogWarning(
                "Remainder charge skipped for booking {BookingId} / payment {PaymentId}: no saved card or customer " +
                "(SavedPaymentMethodId or StripeCustomerId missing). Likely a pre-Phase-2 deposit booking.",
                booking.Id, payment.Id);
            return;
        }

        var remainder = (payment.TotalAmount ?? 0m) - (payment.DepositAmount ?? 0m);
        if (remainder <= 0m)
        {
            _logger.LogWarning(
                "Remainder charge skipped for booking {BookingId} / payment {PaymentId}: computed remainder {Remainder} is not positive.",
                booking.Id, payment.Id, remainder);
            return;
        }

        // Atomic claim (shared with the client on-session pay-remainder flow): only the winner charges. If we
        // can't claim, the client is paying it right now (or it was just resolved) — skip, do not charge.
        var claimed = await _unitOfWork.TryClaimRemainderChargeAsync(payment.Id);
        if (!claimed)
        {
            _logger.LogInformation(
                "Remainder charge skipped for booking {BookingId} / payment {PaymentId}: could not claim it (client pay-remainder in progress or already resolved).",
                booking.Id, payment.Id);
            return;
        }
        payment.Status = PaymentStatus.RemainderCharging; // reflect the claim on the tracked entity

        try
        {
            // Layer 1: the stable idempotency key makes this a no-op-safe operation — a repeat or overlapping
            // call returns the same PaymentIntent instead of charging again.
            var result = await _paymentGatewayService.ChargeOffSessionAsync(new ChargeOffSessionRequest
            {
                AmountInSmallestUnit = StripeAmountConverter.ToSmallestUnit(remainder),
                Currency = _stripeOptions.DefaultCurrency.ToLowerInvariant(),
                CustomerId = client!.StripeCustomerId!,
                PaymentMethodId = payment.SavedPaymentMethodId!,
                IdempotencyKey = $"remainder-{payment.Id}",
                Metadata = new Dictionary<string, string>
                {
                    ["booking_id"] = booking.Id.ToString(),
                    ["payment_id"] = payment.Id.ToString(),
                    ["kind"] = "remainder"
                }
            });

            var recorded = await RecordRemainderPaidAsync(booking, payment, result.PaymentIntentId, remainder, now);
            if (recorded)
            {
                await NotifyRemainderPaidAsync(booking, client!);
            }
        }
        catch (Exception ex)
        {
            // Any failure (insufficient funds, card declined, SCA/authentication_required) is treated the
            // same: land cleanly in RemainderFailed, then notify the client to pay on-session (Phase 3
            // grace). Both the payment and the booking leave the chargeable states, so neither this spec nor
            // the payment gate re-selects it: no charge-spam.
            _logger.LogError(ex,
                "Remainder off-session charge failed for booking {BookingId} / payment {PaymentId}. Marking RemainderFailed.",
                booking.Id, payment.Id);

            var recorded = await RecordRemainderFailedAsync(booking, payment, ex.Message, now);
            if (recorded)
            {
                await NotifyRemainderFailedAsync(booking, client!);
            }
        }
    }

    /// <summary>Returns true only when the FullyPaid transition was committed (so the caller notifies once).</summary>
    private async Task<bool> RecordRemainderPaidAsync(
        BookingRequest booking, Payment payment, string gatewayReference, decimal remainder, DateTimeOffset now)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Compare-and-set (layer 3): we only record if we still hold the claim (RemainderCharging), so a
            // concurrent actor that already resolved this remainder can't produce a duplicate write.
            if (payment.Status != PaymentStatus.RemainderCharging)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return false;
            }

            payment.Status = PaymentStatus.FullyPaid;
            payment.RemainderGatewayReference = gatewayReference;
            payment.RemainderChargedAt = now;
            _unitOfWork.Repository<Payment, long>().Update(payment);

            booking.PaymentStatus = BookingPaymentStatus.Paid;
            booking.UpdatedAt = now;
            _unitOfWork.Repository<BookingRequest, long>().Update(booking);

            var history = new BookingStatusHistory
            {
                BookingRequestId = booking.Id,
                PreviousStatus = booking.Status.ToString(),
                NewStatus = booking.Status.ToString(),
                ChangedByUserId = null,
                Notes = $"remainder charged off-session ({remainder:0.00}); booking is now fully paid"
            };
            await _unitOfWork.Repository<BookingStatusHistory, long>().AddAsync(history);

            await _unitOfWork.CommitTransactionAsync();
            return true;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();

            // The charge succeeded at Stripe but recording it failed. NOT re-charged: the payment stays in
            // DepositPaid_RemainderDue, and the next run re-invokes Stripe with the SAME idempotency key,
            // which returns the same PaymentIntent (no second charge) and records it then.
            _logger.LogCritical(ex,
                "REMAINDER RECONCILIATION NEEDED: off-session charge {GatewayReference} for booking {BookingId} / " +
                "payment {PaymentId} succeeded at Stripe but recording it failed and was rolled back. The stable " +
                "idempotency key remainder-{PaymentId} makes the next run's re-charge safe (no double charge).",
                gatewayReference, booking.Id, payment.Id);
            return false;
        }
    }

    /// <summary>Returns true only when the RemainderFailed transition was committed (so the caller notifies once).</summary>
    private async Task<bool> RecordRemainderFailedAsync(BookingRequest booking, Payment payment, string failureReason, DateTimeOffset now)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Compare-and-set (layer 3): we only transition a payment we still hold the claim on
            // (RemainderCharging), so a run that races a concurrent success/failure can't overwrite it.
            if (payment.Status != PaymentStatus.RemainderCharging)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return false;
            }

            payment.Status = PaymentStatus.RemainderFailed;
            // Column is nvarchar(500); keep the reason within bounds.
            payment.RemainderFailureReason = failureReason.Length > 500 ? failureReason[..500] : failureReason;
            // Start the grace clock: the grace-expiry job (later phase) measures GracePeriodDays from here.
            payment.RemainderFailedAt = now;
            _unitOfWork.Repository<Payment, long>().Update(payment);

            // Booking leaves DepositPaid too, so the selection spec no longer picks it: the job will not
            // retry this booking (no charge-spam). Phase 3 handles notify / grace / resolution.
            booking.PaymentStatus = BookingPaymentStatus.RemainderFailed;
            booking.UpdatedAt = now;
            _unitOfWork.Repository<BookingRequest, long>().Update(booking);

            var history = new BookingStatusHistory
            {
                BookingRequestId = booking.Id,
                PreviousStatus = booking.Status.ToString(),
                NewStatus = booking.Status.ToString(),
                ChangedByUserId = null,
                Notes = "remainder charge failed; booking is awaiting resolution"
            };
            await _unitOfWork.Repository<BookingStatusHistory, long>().AddAsync(history);

            await _unitOfWork.CommitTransactionAsync();
            return true;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex,
                "Failed to record RemainderFailed for booking {BookingId} / payment {PaymentId} after a failed charge.",
                booking.Id, payment.Id);
            return false;
        }
    }

    /// <summary>
    /// Notifies the client (in-app + email) that their booking is now fully paid, and the vendor (in-app)
    /// that a payment was received. Best-effort — the charge is already recorded, so a notification hiccup
    /// must never fail the job.
    /// </summary>
    private async Task NotifyRemainderPaidAsync(BookingRequest booking, Client client)
    {
        await NotifyBestEffortAsync(() => _notificationService.NotifyUserWithEmailAsync(
            client.UserId,
            NotificationTypes.RemainderPaid,
            "Your booking is now fully paid",
            $"The remaining balance for your booking on {booking.EventDate:d} was charged successfully. Your booking is fully paid.",
            emailSubject: "Your booking is fully paid",
            emailBody: $"The remaining balance for your booking on {booking.EventDate:d} was charged successfully. " +
                       "Your booking is now paid in full — thank you."));

        var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(booking.VendorId);
        if (vendor is not null)
        {
            await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
                vendor.UserId,
                NotificationTypes.PaymentReceived,
                "Remaining balance received",
                $"The remaining balance for the booking on {booking.EventDate:d} has been received."));
        }
    }

    /// <summary>
    /// Notifies the client (in-app + email) that the automatic remainder charge failed and they need to
    /// pay to keep the booking. Phase 3 grace/cancellation handles what happens if they don't; this only
    /// tells them. Best-effort.
    /// </summary>
    private async Task NotifyRemainderFailedAsync(BookingRequest booking, Client client)
    {
        await NotifyBestEffortAsync(() => _notificationService.NotifyUserWithEmailAsync(
            client.UserId,
            NotificationTypes.RemainderFailed,
            "We couldn't collect your remaining balance",
            $"We couldn't automatically charge the remaining balance for your booking on {booking.EventDate:d}. " +
            "Please pay it to keep your booking.",
            emailSubject: "Action needed: your remaining balance couldn't be charged",
            emailBody: $"We couldn't automatically charge the remaining balance for your booking on {booking.EventDate:d}. " +
                       "Please pay it as soon as possible to keep your booking."));
    }

    private async Task NotifyBestEffortAsync(Func<Task> notify)
    {
        try
        {
            await notify();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send a remainder-charge notification.");
        }
    }
}
