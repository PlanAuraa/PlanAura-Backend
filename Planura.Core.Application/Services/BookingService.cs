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

public class BookingService : IBookingService
{
    private const string DbUpdateExceptionName = "DbUpdateException";
    private const string ConcurrencyExceptionName = "DbUpdateConcurrencyException";
    private const string SqlExceptionName = "SqlException";
    private const int UniqueConstraintViolationNumber = 2601;
    private const int UniqueIndexViolationNumber = 2627;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly INotificationService _notificationService;
    private readonly IPaymentGatewayService _paymentGatewayService;
    private readonly BookingOptions _bookingOptions;
    private readonly StripeOptions _stripeOptions;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        INotificationService notificationService,
        IPaymentGatewayService paymentGatewayService,
        IOptions<BookingOptions> bookingOptions,
        IOptions<StripeOptions> stripeOptions,
        ILogger<BookingService> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _notificationService = notificationService;
        _paymentGatewayService = paymentGatewayService;
        _bookingOptions = bookingOptions.Value;
        _stripeOptions = stripeOptions.Value;
        _logger = logger;
    }

    public async Task<BookingRequestDto> CreateBookingRequestAsync(long clientUserId, CreateBookingRequestDto dto)
    {
        var clientId = await ResolveClientIdAsync(clientUserId);

        var eventPlan = await _unitOfWork.Repository<EventPlan, long>().GetAsync(dto.EventPlanId);
        if (eventPlan is null || eventPlan.ClientId != clientId)
        {
            throw new NotFoundExeption(nameof(EventPlan), dto.EventPlanId);
        }

        var slotRepo = _unitOfWork.Repository<VendorAvailability, long>();
        var slot = await slotRepo.GetWithSpecAsync(new VendorAvailabilityByIdSpecification(dto.AvailabilityId));
        if (slot is null)
        {
            throw new NotFoundExeption(nameof(VendorAvailability), dto.AvailabilityId);
        }

        if (slot.Status != AvailabilityStatus.Available)
        {
            throw new SlotUnavailableExeption(
                $"Slot {dto.AvailabilityId} is no longer available for booking.");
        }

        var vendor = slot.Vendor;
        if (!VerificationStatus.IsApproved(vendor.VerificationStatus))
        {
            throw new BadRequestExeption("This vendor is not verified and cannot accept bookings.");
        }

        var vendorUser = await _unitOfWork.Repository<ApplicationUser, long>().GetAsync(vendor.UserId);
        if (vendorUser is null || !vendorUser.IsActive)
        {
            throw new BadRequestExeption("This vendor is not currently active.");
        }

        decimal? agreedPrice = null;
        if (dto.VendorPackageId is not null)
        {
            var package = await _unitOfWork.Repository<VendorPackage, long>().GetAsync(dto.VendorPackageId.Value);
            if (package is null || package.VendorId != vendor.Id)
            {
                throw new NotFoundExeption(nameof(VendorPackage), dto.VendorPackageId.Value);
            }

            if (package.MaxGuests is not null && dto.GuestCount is not null && dto.GuestCount > package.MaxGuests)
            {
                throw new BadRequestExeption(
                    $"Guest count exceeds the package maximum of {package.MaxGuests} guests.");
            }

            agreedPrice = package.BasePrice;
        }

        if (agreedPrice is null or <= 0)
        {
            throw new BadRequestExeption("A package must be selected so a price can be authorized for this booking.");
        }

        if (string.IsNullOrWhiteSpace(dto.PaymentMethodId))
        {
            throw new BadRequestExeption("A payment method is required to submit a booking request.");
        }

        if (string.IsNullOrWhiteSpace(dto.RequestId))
        {
            throw new BadRequestExeption("A request id is required to submit a booking request.");
        }

        // Authorize the card hold before touching the database: if this fails (declined, insufficient
        // funds, etc.) no booking request or payment row is ever created — see the option (b) status
        // model discussion. The only failure window left is between this call succeeding and the
        // transaction below committing, which is handled with a compensating cancel in the catch blocks.
        var intentResult = await _paymentGatewayService.AuthorizePaymentIntentAsync(new AuthorizePaymentIntentRequest
        {
            AmountInSmallestUnit = StripeAmountConverter.ToSmallestUnit(agreedPrice.Value),
            Currency = _stripeOptions.DefaultCurrency.ToLowerInvariant(),
            PaymentMethodId = dto.PaymentMethodId,
            IdempotencyKey = dto.RequestId,
            Metadata = new Dictionary<string, string>
            {
                ["client_id"] = clientId.ToString(),
                ["vendor_id"] = vendor.Id.ToString(),
                ["event_plan_id"] = dto.EventPlanId.ToString(),
                ["availability_id"] = dto.AvailabilityId.ToString()
            }
        });

        var booking = new BookingRequest
        {
            EventPlanId = dto.EventPlanId,
            ClientId = clientId,
            VendorId = vendor.Id,
            VendorPackageId = dto.VendorPackageId,
            EventDate = DateOnly.FromDateTime(slot.StartAt.UtcDateTime),
            GuestCount = dto.GuestCount,
            AgreedPrice = agreedPrice,
            ClientMessage = dto.ClientMessage,
            Status = BookingStatus.Pending,
            PaymentStatus = BookingPaymentStatus.Unpaid
        };

        var payment = new Payment
        {
            ClientId = clientId,
            VendorId = vendor.Id,
            Amount = agreedPrice.Value,
            Status = PaymentStatus.Authorized,
            PaymentMethod = dto.PaymentMethodId,
            GatewayReference = intentResult.PaymentIntentId,
            AuthorizedAt = DateTimeOffset.UtcNow,
            BookingRequest = booking
        };

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _unitOfWork.Repository<BookingRequest, long>().AddAsync(booking);
            await _unitOfWork.Repository<Payment, long>().AddAsync(payment);

            slot.Status = AvailabilityStatus.Held;
            slot.HoldExpiresAt = DateTimeOffset.UtcNow.AddHours(_bookingOptions.HoldTtlHours);
            slot.BookingRequest = booking;
            slotRepo.Update(slot);

            var history = new BookingStatusHistory
            {
                BookingRequest = booking,
                PreviousStatus = null,
                NewStatus = BookingStatus.Pending.ToString(),
                ChangedByUserId = clientUserId,
                Notes = "Booking request created by client."
            };
            await _unitOfWork.Repository<BookingStatusHistory, long>().AddAsync(history);

            await _unitOfWork.CommitTransactionAsync();
        }
        catch (Exception ex) when (IsSlotConflict(ex))
        {
            await _unitOfWork.RollbackTransactionAsync();
            await CompensateAuthorizationAsync(intentResult.PaymentIntentId, dto.RequestId);
            throw new SlotUnavailableExeption(
                $"Slot {dto.AvailabilityId} was just taken by another booking request.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            await CompensateAuthorizationAsync(intentResult.PaymentIntentId, dto.RequestId);
            throw;
        }

        await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
            vendor.UserId,
            NotificationTypes.BookingRequestReceived,
            "New booking request",
            $"You have a new booking request for {booking.EventDate:d}."));

        return _mapper.Map<BookingRequestDto>(booking);
    }

    /// <summary>
    /// Best-effort void of a just-created authorization when the DB write that was supposed to record it
    /// fails. Failure to compensate is logged as a distinct, alertable event rather than swallowed, since
    /// it leaves an authorized-but-unrecorded PaymentIntent that needs manual reconciliation in Stripe.
    /// </summary>
    private async Task CompensateAuthorizationAsync(string paymentIntentId, string requestId)
    {
        try
        {
            await _paymentGatewayService.CancelPaymentIntentAsync(new CancelPaymentIntentRequest
            {
                PaymentIntentId = paymentIntentId,
                IdempotencyKey = $"compensate-{requestId}",
                CancellationReason = "abandoned"
            });
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex,
                "ORPHANED STRIPE AUTHORIZATION: failed to compensate-cancel PaymentIntent {PaymentIntentId} " +
                "after its booking request DB write failed (RequestId {RequestId}). This authorization must " +
                "be reconciled manually in Stripe to avoid an unintended capture.",
                paymentIntentId, requestId);
        }
    }

    public async Task<BookingRequestDto> CancelBookingRequestAsync(long bookingRequestId, long clientUserId)
    {
        var clientId = await ResolveClientIdAsync(clientUserId);

        var repo = _unitOfWork.Repository<BookingRequest, long>();
        var booking = await repo.GetAsync(bookingRequestId);
        if (booking is null || booking.ClientId != clientId)
        {
            throw new NotFoundExeption(nameof(BookingRequest), bookingRequestId);
        }

        if (booking.Status != BookingStatus.Pending)
        {
            throw new BadRequestExeption(
                $"Cannot cancel a booking request with status '{booking.Status}'. Only pending requests can be cancelled.");
        }

        var previousStatus = booking.Status;
        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTimeOffset.UtcNow;
        booking.UpdatedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            repo.Update(booking);

            var holdRepo = _unitOfWork.Repository<VendorAvailability, long>();
            var holds = await holdRepo.GetAllWithSpecAsync(
                new VendorAvailabilityByBookingRequestSpecification(bookingRequestId));
            holdRepo.DeleteRange(holds);

            var history = new BookingStatusHistory
            {
                BookingRequestId = bookingRequestId,
                PreviousStatus = previousStatus.ToString(),
                NewStatus = BookingStatus.Cancelled.ToString(),
                ChangedByUserId = clientUserId,
                Notes = "Cancelled by client."
            };
            await _unitOfWork.Repository<BookingStatusHistory, long>().AddAsync(history);

            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }

        var authorizedPayment = await _unitOfWork.Repository<Payment, long>()
            .GetWithSpecAsync(new AuthorizedPaymentByBookingRequestSpecification(bookingRequestId));
        if (authorizedPayment is not null)
        {
            await VoidAuthorizationBestEffortAsync(authorizedPayment, "booking cancelled by client");
        }

        var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(booking.VendorId);
        if (vendor is not null)
        {
            await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
                vendor.UserId,
                NotificationTypes.BookingCancelled,
                "Booking request cancelled",
                $"The client cancelled their booking request for {booking.EventDate:d}."));
        }

        return _mapper.Map<BookingRequestDto>(booking);
    }

    public async Task<BookingRequestDto> AcceptBookingRequestAsync(long bookingRequestId, long vendorUserId)
    {
        var vendorId = await ResolveVendorIdAsync(vendorUserId);

        var repo = _unitOfWork.Repository<BookingRequest, long>();
        var booking = await repo.GetAsync(bookingRequestId);
        if (booking is null || booking.VendorId != vendorId)
        {
            throw new NotFoundExeption(nameof(BookingRequest), bookingRequestId);
        }

        if (booking.Status != BookingStatus.Pending)
        {
            throw new BadRequestExeption(
                $"Cannot accept a booking request with status '{booking.Status}'. Only pending requests can be accepted.");
        }

        var paymentRepo = _unitOfWork.Repository<Payment, long>();
        var payment = await paymentRepo.GetWithSpecAsync(new AuthorizedPaymentByBookingRequestSpecification(bookingRequestId));
        if (payment is null)
        {
            throw new BadRequestExeption("No authorized payment was found for this booking request; it cannot be accepted.");
        }

        try
        {
            var captureResult = await _paymentGatewayService.CapturePaymentIntentAsync(new CapturePaymentIntentRequest
            {
                PaymentIntentId = payment.GatewayReference!,
                IdempotencyKey = $"capture-{payment.Id}"
            });

            if (captureResult.Status != "succeeded")
            {
                throw new InvalidOperationException($"Unexpected PaymentIntent status after capture: {captureResult.Status}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "CAPTURE FAILED: PaymentIntent {PaymentIntentId} for booking request {BookingRequestId} could not be " +
                "captured on vendor accept. Auto-declining the booking.",
                payment.GatewayReference, bookingRequestId);

            await AutoDeclineAfterCaptureFailureAsync(booking, payment);

            throw new PaymentDeclinedExeption(
                "The client's payment could not be captured, so this booking request was automatically declined. " +
                "Ask the client to submit a new request with a valid payment method.");
        }

        var previousStatus = booking.Status;
        booking.Status = BookingStatus.Accepted;
        booking.PaymentStatus = BookingPaymentStatus.Paid;
        booking.RespondedAt = DateTimeOffset.UtcNow;
        booking.UpdatedAt = DateTimeOffset.UtcNow;

        payment.Status = PaymentStatus.Completed;
        payment.PaidAt = DateTimeOffset.UtcNow;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            repo.Update(booking);
            paymentRepo.Update(payment);

            var holdRepo = _unitOfWork.Repository<VendorAvailability, long>();
            var holds = await holdRepo.GetAllWithSpecAsync(
                new VendorAvailabilityByBookingRequestSpecification(bookingRequestId));
            foreach (var hold in holds)
            {
                hold.Status = AvailabilityStatus.Booked;
                hold.HoldExpiresAt = null;
                holdRepo.Update(hold);
            }

            var history = new BookingStatusHistory
            {
                BookingRequestId = bookingRequestId,
                PreviousStatus = previousStatus.ToString(),
                NewStatus = BookingStatus.Accepted.ToString(),
                ChangedByUserId = vendorUserId,
                Notes = "Accepted by vendor; payment captured."
            };
            await _unitOfWork.Repository<BookingStatusHistory, long>().AddAsync(history);

            await _unitOfWork.CommitTransactionAsync();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();

            // The Stripe capture above already succeeded — the client has been charged — but the DB write
            // recording it failed and was rolled back. This is NOT safe to silently retry or reverse here:
            // no automatic refund is attempted. The payment_intent.succeeded webhook (fired regardless of
            // this DB outcome) is expected to reconcile Payment/BookingRequest state; this log is the signal
            // to verify that reconciliation actually happened if it doesn't self-heal.
            _logger.LogCritical(ex,
                "DB WRITE FAILED AFTER SUCCESSFUL CAPTURE: PaymentIntent {PaymentIntentId} for booking request " +
                "{BookingRequestId} was captured in Stripe but the DB transaction recording Accepted/Paid failed " +
                "and was rolled back. Verify the payment_intent.succeeded webhook reconciles this.",
                payment.GatewayReference, bookingRequestId);
            throw;
        }

        var client = await _unitOfWork.Repository<Client, long>().GetAsync(booking.ClientId);
        if (client is not null)
        {
            await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
                client.UserId,
                NotificationTypes.BookingAccepted,
                "Booking request accepted",
                $"Your booking request for {booking.EventDate:d} was accepted and your payment has been processed."));
        }

        return _mapper.Map<BookingRequestDto>(booking);
    }

    public async Task<BookingRequestDto> RejectBookingRequestAsync(long bookingRequestId, long vendorUserId, string? reason)
    {
        var vendorId = await ResolveVendorIdAsync(vendorUserId);

        var repo = _unitOfWork.Repository<BookingRequest, long>();
        var booking = await repo.GetAsync(bookingRequestId);
        if (booking is null || booking.VendorId != vendorId)
        {
            throw new NotFoundExeption(nameof(BookingRequest), bookingRequestId);
        }

        if (booking.Status != BookingStatus.Pending)
        {
            throw new BadRequestExeption(
                $"Cannot reject a booking request with status '{booking.Status}'. Only pending requests can be rejected.");
        }

        var previousStatus = booking.Status;
        booking.Status = BookingStatus.Rejected;
        booking.VendorResponse = reason;
        booking.RespondedAt = DateTimeOffset.UtcNow;
        booking.UpdatedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            repo.Update(booking);

            var holdRepo = _unitOfWork.Repository<VendorAvailability, long>();
            var holds = await holdRepo.GetAllWithSpecAsync(
                new VendorAvailabilityByBookingRequestSpecification(bookingRequestId));
            holdRepo.DeleteRange(holds);

            var history = new BookingStatusHistory
            {
                BookingRequestId = bookingRequestId,
                PreviousStatus = previousStatus.ToString(),
                NewStatus = BookingStatus.Rejected.ToString(),
                ChangedByUserId = vendorUserId,
                Notes = string.IsNullOrWhiteSpace(reason) ? "Rejected by vendor." : $"Rejected by vendor: {reason}"
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
            .GetWithSpecAsync(new AuthorizedPaymentByBookingRequestSpecification(bookingRequestId));
        if (payment is not null)
        {
            await VoidAuthorizationBestEffortAsync(payment, "booking rejected by vendor");
        }

        var client = await _unitOfWork.Repository<Client, long>().GetAsync(booking.ClientId);
        if (client is not null)
        {
            await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
                client.UserId,
                NotificationTypes.BookingRejected,
                "Booking request declined",
                $"The vendor declined your booking request for {booking.EventDate:d}."));
        }

        return _mapper.Map<BookingRequestDto>(booking);
    }

    private async Task AutoDeclineAfterCaptureFailureAsync(BookingRequest booking, Payment payment)
    {
        var previousStatus = booking.Status;
        booking.Status = BookingStatus.Rejected;
        booking.VendorResponse = "Automatically declined: payment capture failed.";
        booking.RespondedAt = DateTimeOffset.UtcNow;
        booking.UpdatedAt = DateTimeOffset.UtcNow;

        payment.Status = PaymentStatus.Failed;

        var repo = _unitOfWork.Repository<BookingRequest, long>();
        var paymentRepo = _unitOfWork.Repository<Payment, long>();

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            repo.Update(booking);
            paymentRepo.Update(payment);

            var holdRepo = _unitOfWork.Repository<VendorAvailability, long>();
            var holds = await holdRepo.GetAllWithSpecAsync(
                new VendorAvailabilityByBookingRequestSpecification(booking.Id));
            holdRepo.DeleteRange(holds);

            var history = new BookingStatusHistory
            {
                BookingRequestId = booking.Id,
                PreviousStatus = previousStatus.ToString(),
                NewStatus = BookingStatus.Rejected.ToString(),
                ChangedByUserId = null,
                Notes = "auto-declined: payment capture failed on vendor accept"
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
                NotificationTypes.BookingRejected,
                "Booking request declined",
                $"Your payment could not be completed for the booking request on {booking.EventDate:d}, so it was " +
                "automatically declined. Please submit a new request with a valid payment method."));
        }
    }

    public async Task<BookingRequestDto> GetBookingRequestAsync(long bookingRequestId, long clientUserId)
    {
        var clientId = await ResolveClientIdAsync(clientUserId);

        var booking = await _unitOfWork.Repository<BookingRequest, long>().GetAsync(bookingRequestId);
        if (booking is null || booking.ClientId != clientId)
        {
            throw new NotFoundExeption(nameof(BookingRequest), bookingRequestId);
        }

        return _mapper.Map<BookingRequestDto>(booking);
    }

    public async Task<PagedResult<BookingRequestDto>> ListMyBookingRequestsAsync(long clientUserId, BookingRequestFilterDto filter)
    {
        var clientId = await ResolveClientIdAsync(clientUserId);

        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;
        var skip = (page - 1) * pageSize;

        var repo = _unitOfWork.Repository<BookingRequest, long>();

        var totalCount = await repo.GetCountAsync(
            new BookingRequestsByClientSpecification(clientId, filter.Status, skip: null, take: null));

        var items = await repo.GetAllWithSpecAsync(
            new BookingRequestsByClientSpecification(clientId, filter.Status, skip, pageSize));

        return new PagedResult<BookingRequestDto>
        {
            Items = _mapper.Map<List<BookingRequestDto>>(items),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<BookingRequestDto> FlagDisputeAsync(long bookingRequestId, long userId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BadRequestExeption("A dispute reason is required.");
        }

        var clientId = await ResolveClientIdAsync(userId);

        var repo = _unitOfWork.Repository<BookingRequest, long>();
        var booking = await repo.GetAsync(bookingRequestId);
        if (booking is null || booking.ClientId != clientId)
        {
            throw new NotFoundExeption(nameof(BookingRequest), bookingRequestId);
        }

        if (booking.Status is not (BookingStatus.Accepted or BookingStatus.Completed))
        {
            throw new BadRequestExeption(
                $"Cannot raise a dispute on a booking request with status '{booking.Status}'.");
        }

        if (booking.DisputeStatus == DisputeStatus.Open)
        {
            throw new BadRequestExeption("A dispute is already open for this booking request.");
        }

        booking.DisputeStatus = DisputeStatus.Open;
        booking.DisputedAt = DateTimeOffset.UtcNow;
        booking.DisputedByUserId = userId;
        booking.UpdatedAt = DateTimeOffset.UtcNow;

        repo.Update(booking);

        var history = new BookingStatusHistory
        {
            BookingRequestId = bookingRequestId,
            PreviousStatus = booking.Status.ToString(),
            NewStatus = booking.Status.ToString(),
            ChangedByUserId = userId,
            Notes = $"Dispute raised: {reason}"
        };
        await _unitOfWork.Repository<BookingStatusHistory, long>().AddAsync(history);

        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<BookingRequestDto>(booking);
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

    private async Task<long> ResolveVendorIdAsync(long userId)
    {
        var vendor = await _unitOfWork.Repository<Vendor, long>()
            .GetWithSpecAsync(new VendorByUserIdSpecification(userId));

        if (vendor is null)
        {
            throw new NotFoundExeption(nameof(Vendor), userId);
        }

        return vendor.Id;
    }

    /// <summary>
    /// Best-effort void of an authorized-but-not-yet-captured PaymentIntent when a booking request is
    /// rejected, cancelled, or expires. Unlike the capture path, a failure here is low-stakes: no money has
    /// moved, and Stripe releases an un-captured authorization hold on its own after its expiry window, so a
    /// failed void here is logged as a warning (for reconciliation/cleanup) rather than blocking the caller.
    /// </summary>
    private async Task VoidAuthorizationBestEffortAsync(Payment payment, string context)
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
                "Failed to void Stripe authorization for PaymentIntent {PaymentIntentId} ({Context}). The hold will " +
                "still expire naturally on Stripe's side; Payment {PaymentId} remains marked Authorized locally until reconciled.",
                payment.GatewayReference, context, payment.Id);
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
            // Notifications are best-effort and must never fail the booking action itself.
        }
    }

    private static bool IsSlotConflict(Exception ex)
    {
        if (ex.GetType().Name == ConcurrencyExceptionName)
        {
            return true;
        }

        if (ex.GetType().Name != DbUpdateExceptionName || ex.InnerException is null)
        {
            return false;
        }

        var innerException = ex.InnerException;
        if (innerException.GetType().Name != SqlExceptionName)
        {
            return false;
        }

        var number = innerException.GetType().GetProperty("Number")?.GetValue(innerException) as int?;
        return number is UniqueConstraintViolationNumber or UniqueIndexViolationNumber;
    }
}
