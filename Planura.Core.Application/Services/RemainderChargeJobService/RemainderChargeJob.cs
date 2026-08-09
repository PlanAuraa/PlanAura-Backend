using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Planura.Core.Application.Abstraction.PaymentGateway;
using Planura.Core.Application.Common;
using Planura.Core.Application.Specifications;
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
    private readonly BookingOptions _bookingOptions;
    private readonly StripeOptions _stripeOptions;
    private readonly ILogger<RemainderChargeJob> _logger;

    public RemainderChargeJob(
        IUnitOfWork unitOfWork,
        IPaymentGatewayService paymentGatewayService,
        IOptions<BookingOptions> bookingOptions,
        IOptions<StripeOptions> stripeOptions,
        ILogger<RemainderChargeJob> logger)
    {
        _unitOfWork = unitOfWork;
        _paymentGatewayService = paymentGatewayService;
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

            await RecordRemainderPaidAsync(booking, payment, result.PaymentIntentId, remainder, now);
        }
        catch (Exception ex)
        {
            // Any failure (insufficient funds, card declined, SCA/authentication_required) is treated the
            // same: land cleanly in RemainderFailed. No notifications / grace / retry here — Phase 3 owns
            // resolution. Both the payment and the booking leave the chargeable states, so neither this
            // spec nor the payment gate re-selects it: no charge-spam.
            _logger.LogError(ex,
                "Remainder off-session charge failed for booking {BookingId} / payment {PaymentId}. Marking RemainderFailed.",
                booking.Id, payment.Id);

            await RecordRemainderFailedAsync(booking, payment, ex.Message, now);
        }
    }

    private async Task RecordRemainderPaidAsync(
        BookingRequest booking, Payment payment, string gatewayReference, decimal remainder, DateTimeOffset now)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Compare-and-set (layer 3): only record if the payment is still in the chargeable state, so a
            // concurrent run that already recorded this remainder can't produce a duplicate write.
            if (payment.Status != PaymentStatus.DepositPaid_RemainderDue)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return;
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
        }
    }

    private async Task RecordRemainderFailedAsync(BookingRequest booking, Payment payment, string failureReason, DateTimeOffset now)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Compare-and-set (layer 3): only transition a payment that is still chargeable, so a run that
            // races a concurrent success/failure can't overwrite the resolved state.
            if (payment.Status != PaymentStatus.DepositPaid_RemainderDue)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return;
            }

            payment.Status = PaymentStatus.RemainderFailed;
            // Column is nvarchar(500); keep the reason within bounds.
            payment.RemainderFailureReason = failureReason.Length > 500 ? failureReason[..500] : failureReason;
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
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex,
                "Failed to record RemainderFailed for booking {BookingId} / payment {PaymentId} after a failed charge.",
                booking.Id, payment.Id);
        }
    }
}
