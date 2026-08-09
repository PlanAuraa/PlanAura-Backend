using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Planura.Core.Application.Abstraction.PaymentGateway;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly INotificationService _notificationService;
    private readonly IPaymentGatewayService _paymentGatewayService;
    private readonly StripeOptions _stripeOptions;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        INotificationService notificationService,
        IPaymentGatewayService paymentGatewayService,
        IOptions<StripeOptions> stripeOptions,
        ILogger<PaymentService> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _notificationService = notificationService;
        _paymentGatewayService = paymentGatewayService;
        _stripeOptions = stripeOptions.Value;
        _logger = logger;
    }

    public async Task HandleStripeWebhookAsync(string rawJson, string stripeSignatureHeader)
    {
        var gatewayEvent = _paymentGatewayService.ConstructWebhookEvent(rawJson, stripeSignatureHeader);

        if (string.IsNullOrEmpty(gatewayEvent.PaymentIntentId))
        {
            return;
        }

        var payment = await _unitOfWork.Repository<Payment, long>()
            .GetWithSpecAsync(new PaymentByGatewayReferenceSpecification(gatewayEvent.PaymentIntentId));

        // The deposit PI didn't match — try the remainder PI (a different id, stored separately). This is
        // how an on-session (SCA) remainder payment completed in the client's browser gets reconciled.
        var matchedByRemainderReference = false;
        if (payment is null)
        {
            payment = await _unitOfWork.Repository<Payment, long>()
                .GetWithSpecAsync(new PaymentByRemainderGatewayReferenceSpecification(gatewayEvent.PaymentIntentId));
            matchedByRemainderReference = payment is not null;
        }

        if (payment is null)
        {
            if (gatewayEvent.Type == "payment_intent.amount_capturable_updated")
            {
                // The synchronous AuthorizePaymentIntentAsync call in CreateBookingRequestAsync normally
                // already recorded this Payment row before this webhook arrives. Getting here with no
                // matching row means that synchronous response was lost (crash/timeout) after Stripe
                // authorized the card — the booking/payment were never created (see option (b) in the
                // authorize-first design). This is informational/logging only; no auto-recovery is attempted
                // since there's no BookingRequestId to attach it to, only the metadata captured at authorize time.
                var metadata = gatewayEvent.Metadata is null
                    ? "none"
                    : string.Join(", ", gatewayEvent.Metadata.Select(kv => $"{kv.Key}={kv.Value}"));

                _logger.LogWarning(
                    "ORPHANED STRIPE AUTHORIZATION: PaymentIntent {PaymentIntentId} became capturable in Stripe " +
                    "but no matching Payment record exists locally. Likely a lost response after the synchronous " +
                    "authorize call. Metadata: {Metadata}. Needs manual reconciliation in Stripe.",
                    gatewayEvent.PaymentIntentId, metadata);
            }

            return;
        }

        switch (gatewayEvent.Type)
        {
            case "payment_intent.succeeded":
                if (matchedByRemainderReference)
                {
                    await HandleRemainderSucceededAsync(payment);
                }
                else
                {
                    await HandlePaymentSucceededAsync(payment);
                }
                break;
            case "payment_intent.payment_failed":
                if (matchedByRemainderReference)
                {
                    await HandleRemainderFailedAsync(payment);
                }
                else
                {
                    await HandlePaymentFailedAsync(payment);
                }
                break;
            case "payment_intent.canceled":
                await HandlePaymentCanceledAsync(payment);
                break;
            case "payment_intent.amount_capturable_updated":
                // Payment already exists (created by the synchronous authorize call) — nothing to reconcile.
                break;
            case "charge.refunded":
                await HandleChargeRefundedAsync(payment);
                break;
        }
    }

    public async Task<PagedResult<PaymentDto>> ListMyTransactionsAsync(long userId, TransactionsFilterDto filter)
    {
        var clientId = await ResolveClientIdAsync(userId);

        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;
        var skip = (page - 1) * pageSize;

        var repo = _unitOfWork.Repository<Payment, long>();

        var totalCount = await repo.GetCountAsync(
            new PaymentsByClientSpecification(clientId, filter.Status, skip: null, take: null));

        var items = await repo.GetAllWithSpecAsync(
            new PaymentsByClientSpecification(clientId, filter.Status, skip, pageSize));

        return new PagedResult<PaymentDto>
        {
            Items = _mapper.Map<List<PaymentDto>>(items),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private async Task HandlePaymentSucceededAsync(Payment payment)
    {
        // Both are already-captured, already-recorded resting states (full path and deposit path
        // respectively): the vendor-accept flow handled them synchronously — the common case. Re-processing
        // here would just re-send notifications or, worse, mark a deposit booking fully Paid, so no-op.
        if (payment.Status is PaymentStatus.Completed or PaymentStatus.DepositPaid_RemainderDue)
        {
            return;
        }

        // Only reachable when the payment was still an uncaptured hold (Authorized / DepositAuthorized):
        // AcceptBookingRequestAsync's Stripe capture succeeded but its DB transaction failed and rolled back
        // (see the LogCritical there). This webhook is the durable backstop that reconciles
        // Payment/BookingPaymentStatus to the correct captured state for the path; BookingRequest.Status
        // itself is NOT touched here and may still be stuck at Pending — flagged below for manual follow-up.
        var wasReconciliation = payment.Status is PaymentStatus.Authorized or PaymentStatus.DepositAuthorized;

        // A deposit hold captures to "deposit paid, remainder due" (Phase 1 has no remainder mechanism yet),
        // never to fully paid; a full hold captures to Completed/Paid exactly as before.
        payment.Status = payment.IsDeposit ? PaymentStatus.DepositPaid_RemainderDue : PaymentStatus.Completed;
        payment.PaidAt ??= DateTimeOffset.UtcNow;
        _unitOfWork.Repository<Payment, long>().Update(payment);

        var bookingRepo = _unitOfWork.Repository<BookingRequest, long>();
        var booking = await bookingRepo.GetAsync(payment.BookingRequestId);
        if (booking is not null)
        {
            booking.PaymentStatus = payment.IsDeposit ? BookingPaymentStatus.DepositPaid : BookingPaymentStatus.Paid;
            booking.UpdatedAt = DateTimeOffset.UtcNow;
            bookingRepo.Update(booking);

            if (wasReconciliation && booking.Status != BookingStatus.Accepted)
            {
                _logger.LogCritical(
                    "RECONCILIATION NEEDED: PaymentIntent {PaymentIntentId} succeeded per Stripe webhook but " +
                    "BookingRequest {BookingRequestId} is still '{Status}', not Accepted. PaymentStatus has been " +
                    "reconciled to Paid; BookingRequest.Status needs manual review.",
                    payment.GatewayReference, booking.Id, booking.Status);
            }
        }

        await _unitOfWork.SaveChangesAsync();

        var client = await _unitOfWork.Repository<Client, long>().GetAsync(payment.ClientId);
        if (client is not null)
        {
            await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
                client.UserId,
                NotificationTypes.PaymentSuccessful,
                "Payment successful",
                "Your payment was processed successfully."));
        }

        var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(payment.VendorId);
        if (vendor is not null)
        {
            await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
                vendor.UserId,
                NotificationTypes.PaymentReceived,
                "Payment received",
                "You received a payment for a booking."));
        }
    }

    /// <summary>
    /// Reconciles a successful on-session remainder PaymentIntent (deposit path, Phase 3) — the SCA the
    /// client completed in the browser. Transitions to FullyPaid, ends the grace window, and notifies. The
    /// synchronous PayRemainderAsync path may have already recorded FullyPaid, in which case this no-ops.
    /// </summary>
    private async Task HandleRemainderSucceededAsync(Payment payment)
    {
        if (payment.Status == PaymentStatus.FullyPaid)
        {
            return; // already finalized synchronously — avoid duplicate notifications
        }

        var now = DateTimeOffset.UtcNow;
        payment.Status = PaymentStatus.FullyPaid;
        payment.PaidAt ??= now;
        payment.RemainderChargedAt ??= now;
        payment.RemainderFailedAt = null;       // grace ends
        payment.RemainderFailureReason = null;
        _unitOfWork.Repository<Payment, long>().Update(payment);

        var booking = await _unitOfWork.Repository<BookingRequest, long>().GetAsync(payment.BookingRequestId);
        if (booking is not null)
        {
            booking.PaymentStatus = BookingPaymentStatus.Paid;
            booking.UpdatedAt = now;
            _unitOfWork.Repository<BookingRequest, long>().Update(booking);
        }

        await _unitOfWork.SaveChangesAsync();

        var client = await _unitOfWork.Repository<Client, long>().GetAsync(payment.ClientId);
        if (client is not null)
        {
            await NotifyBestEffortAsync(() => _notificationService.NotifyUserWithEmailAsync(
                client.UserId,
                NotificationTypes.RemainderPaid,
                "Your booking is now fully paid",
                "Your payment for the remaining balance was successful. Your booking is fully paid.",
                emailSubject: "Your booking is fully paid",
                emailBody: "Your payment for the remaining balance was successful. Your booking is now paid in full — thank you."));
        }

        var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(payment.VendorId);
        if (vendor is not null)
        {
            await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
                vendor.UserId,
                NotificationTypes.PaymentReceived,
                "Remaining balance received",
                "The remaining balance for a booking has been received."));
        }
    }

    /// <summary>
    /// Reconciles a failed on-session remainder PaymentIntent (e.g. the client's SCA authentication failed).
    /// Transitions to RemainderFailed and (re)starts the grace window, unless already resolved.
    /// </summary>
    private async Task HandleRemainderFailedAsync(Payment payment)
    {
        if (payment.Status is PaymentStatus.FullyPaid or PaymentStatus.RemainderFailed)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        payment.Status = PaymentStatus.RemainderFailed;
        payment.RemainderFailedAt ??= now;
        payment.RemainderFailureReason ??= "On-session remainder payment was not completed.";
        _unitOfWork.Repository<Payment, long>().Update(payment);

        var booking = await _unitOfWork.Repository<BookingRequest, long>().GetAsync(payment.BookingRequestId);
        if (booking is not null)
        {
            booking.PaymentStatus = BookingPaymentStatus.RemainderFailed;
            booking.UpdatedAt = now;
            _unitOfWork.Repository<BookingRequest, long>().Update(booking);
        }

        await _unitOfWork.SaveChangesAsync();

        var client = await _unitOfWork.Repository<Client, long>().GetAsync(payment.ClientId);
        if (client is not null)
        {
            await NotifyBestEffortAsync(() => _notificationService.NotifyUserWithEmailAsync(
                client.UserId,
                NotificationTypes.RemainderFailed,
                "We couldn't collect your remaining balance",
                "Your remaining balance payment wasn't completed. Please try again to keep your booking.",
                emailSubject: "Action needed: your remaining balance wasn't paid",
                emailBody: "Your remaining balance payment wasn't completed. Please try again as soon as possible to keep your booking."));
        }
    }

    private async Task HandlePaymentFailedAsync(Payment payment)
    {
        payment.Status = PaymentStatus.Failed;
        _unitOfWork.Repository<Payment, long>().Update(payment);
        await _unitOfWork.SaveChangesAsync();

        var client = await _unitOfWork.Repository<Client, long>().GetAsync(payment.ClientId);
        if (client is not null)
        {
            await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
                client.UserId,
                NotificationTypes.PaymentFailed,
                "Payment failed",
                "Your payment could not be processed. Please try again."));
        }
    }

    private async Task HandlePaymentCanceledAsync(Payment payment)
    {
        if (payment.Status is PaymentStatus.Cancelled or PaymentStatus.Completed or PaymentStatus.Refunded)
        {
            // Already reconciled locally by our own void call (the common case), or a stale/out-of-order
            // event for a payment that has since moved past Authorized — nothing to do either way.
            return;
        }

        payment.Status = PaymentStatus.Cancelled;
        payment.CancelledAt = DateTimeOffset.UtcNow;
        _unitOfWork.Repository<Payment, long>().Update(payment);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task HandleChargeRefundedAsync(Payment payment)
    {
        payment.Status = PaymentStatus.Refunded;
        payment.RefundedAt = DateTimeOffset.UtcNow;
        _unitOfWork.Repository<Payment, long>().Update(payment);

        var bookingRepo = _unitOfWork.Repository<BookingRequest, long>();
        var booking = await bookingRepo.GetAsync(payment.BookingRequestId);
        if (booking is not null)
        {
            booking.PaymentStatus = BookingPaymentStatus.Refunded;
            booking.UpdatedAt = DateTimeOffset.UtcNow;
            bookingRepo.Update(booking);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<long> ResolveClientIdAsync(long userId)
    {
        var client = await _unitOfWork.Repository<Client, long>()
            .GetWithSpecAsync(new ClientByUserIdSpecification(userId));

        if (client is null)
        {
            throw new NotFoundExeption(nameof(Client), userId);
        }

        return client.Id;
    }

    private static async Task NotifyBestEffortAsync(Func<Task> notify)
    {
        try
        {
            await notify();
        }
        catch
        {
            // Notifications are best-effort and must never fail webhook processing.
        }
    }
}
