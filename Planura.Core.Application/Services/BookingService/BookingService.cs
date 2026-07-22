using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Planura.Core.Application.Abstraction.AttachementService;
using Planura.Core.Application.Abstraction.PaymentGateway;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services.Booking;
using Planura.Core.Application.Services.Contract;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;
using System.Text.Json;

namespace Planura.Core.Application.Services;

public class BookingService : IBookingService
{
    private const string DbUpdateExceptionName = "DbUpdateException";
    private const string ConcurrencyExceptionName = "DbUpdateConcurrencyException";
    private const string SqlExceptionName = "SqlException";
    private const int UniqueConstraintViolationNumber = 2601;
    private const int UniqueIndexViolationNumber = 2627;

    private const string BookingContractsFolder = "booking-contracts";
    private const string VendorPartnershipAgreementsFolder = "vendor-partnership-agreements";

    private static readonly JsonSerializerOptions NotificationDataJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly INotificationService _notificationService;
    private readonly IPaymentGatewayService _paymentGatewayService;
    private readonly IContractService _contractService;
    private readonly IAttachmentService _attachmentService;
    private readonly BookingOptions _bookingOptions;
    private readonly StripeOptions _stripeOptions;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        INotificationService notificationService,
        IPaymentGatewayService paymentGatewayService,
        IContractService contractService,
        IAttachmentService attachmentService,
        IOptions<BookingOptions> bookingOptions,
        IOptions<StripeOptions> stripeOptions,
        ILogger<BookingService> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _notificationService = notificationService;
        _paymentGatewayService = paymentGatewayService;
        _contractService = contractService;
        _attachmentService = attachmentService;
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
            if (slot.BookingRequest is not null && slot.BookingRequest.ClientId == clientId)
            {
                throw new SlotUnavailableExeption("You already have a booking for this exact time slot.");
            }

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

        return MapBooking(booking);
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
            foreach (var hold in holds)
            {
                hold.Status = AvailabilityStatus.Available;
                hold.BookingRequestId = null;
                hold.BookingRequest = null;
                hold.HoldExpiresAt = null;
                holdRepo.Update(hold);
            }

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

        return MapBooking(booking);
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

        // Step 2-4 of the AI contract workflow: generate the Event Booking Contract, generate the
        // vendor's one-time Partnership Agreement if it doesn't have one yet, persist both via the
        // existing attachment storage, and notify client/vendor/admin. All best-effort: the payment
        // has already been captured above, so a Gemini/PDF hiccup here must never fail this call or
        // undo the accepted booking.
        var client = await _unitOfWork.Repository<Client, long>().GetAsync(booking.ClientId);
        await GenerateContractsAndNotifyAsync(booking, client, vendorUserId);

        return MapBooking(booking);
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
            foreach (var hold in holds)
            {
                hold.Status = AvailabilityStatus.Available;
                hold.BookingRequestId = null;
                hold.BookingRequest = null;
                hold.HoldExpiresAt = null;
                holdRepo.Update(hold);
            }

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

        return MapBooking(booking);
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
            foreach (var hold in holds)
            {
                hold.Status = AvailabilityStatus.Available;
                hold.BookingRequestId = null;
                hold.BookingRequest = null;
                hold.HoldExpiresAt = null;
                holdRepo.Update(hold);
            }

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

        // The capture attempt failed, but the PaymentIntent may still be live (requires_capture) on Stripe's
        // side — this is what left pi_3Tu1cdJTBgcTyrCL0osgfeOY orphaned in production/test. Void it the same
        // way Reject/Cancel do, so the hold on the client's card is actually released.
        await VoidAuthorizationBestEffortAsync(payment, "payment capture failed on vendor accept");

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

        return MapBooking(booking);
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
            Items = MapBookings(items),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<BookingRequestDto>> ListVendorBookingRequestsAsync(long vendorUserId, BookingRequestFilterDto filter)
    {
        var vendorId = await ResolveVendorIdAsync(vendorUserId);

        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;
        var skip = (page - 1) * pageSize;

        var repo = _unitOfWork.Repository<BookingRequest, long>();

        var totalCount = await repo.GetCountAsync(
            new BookingRequestsByVendorSpecification(vendorId, filter.Status, skip: null, take: null));

        var items = await repo.GetAllWithSpecAsync(
            new BookingRequestsByVendorSpecification(vendorId, filter.Status, skip, pageSize));

        return new PagedResult<BookingRequestDto>
        {
            Items = MapBookings(items),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<BookingRequestDto> GetVendorBookingRequestAsync(long bookingRequestId, long vendorUserId)
    {
        var vendorId = await ResolveVendorIdAsync(vendorUserId);

        var booking = await _unitOfWork.Repository<BookingRequest, long>().GetAsync(bookingRequestId);
        if (booking is null || booking.VendorId != vendorId)
        {
            throw new NotFoundExeption(nameof(BookingRequest), bookingRequestId);
        }

        return MapBooking(booking);
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

        return MapBooking(booking);
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

    private BookingRequestDto MapBooking(BookingRequest booking)
    {
        var dto = _mapper.Map<BookingRequestDto>(booking);
        dto.ContractDocumentUrl = _attachmentService.ToAbsoluteUrl(dto.ContractDocumentUrl);
        dto.ClientName = booking.Client?.User?.FullName;
        return dto;
    }

    private List<BookingRequestDto> MapBookings(IEnumerable<BookingRequest> bookings)
    {
        return bookings.Select(MapBooking).ToList();
    }

    /// <summary>
    /// Orchestrates the AI contract workflow that follows a vendor accepting (confirming) a booking:
    /// draft + store the Client/Vendor Event Booking Contract, draft + store the Vendor/Planura
    /// Partnership Agreement exactly once per vendor, and notify client, vendor and admins. Every
    /// step here is best-effort - the booking is already Accepted/Paid by the time this runs, so
    /// nothing in here is allowed to throw back to the caller.
    /// </summary>
    private async Task GenerateContractsAndNotifyAsync(BookingRequest booking, Client? client, long vendorUserId)
    {
        var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(booking.VendorId);
        if (vendor is null)
        {
            return;
        }

        var vendorUser = await _unitOfWork.Repository<ApplicationUser, long>().GetAsync(vendor.UserId);
        var eventPlan = await _unitOfWork.Repository<EventPlan, long>().GetAsync(booking.EventPlanId);
        var clientUser = client is not null
            ? await _unitOfWork.Repository<ApplicationUser, long>().GetAsync(client.UserId)
            : null;

        string? contractUrl = null;
        if (vendor is not null && vendorUser is not null && eventPlan is not null && client is not null && clientUser is not null)
        {
            contractUrl = await GenerateBookingContractBestEffortAsync(booking, vendor, eventPlan, client, clientUser, vendorUser);
        }
        else
        {
            _logger.LogWarning(
                "Skipping Event Booking Contract generation for booking request {BookingRequestId}: vendor, vendor user, client, client user, or event plan could not be loaded.",
                booking.Id);
        }

        if (contractUrl is not null)
        {
            if (client is not null)
            {
                await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
                    client.UserId,
                    NotificationTypes.ContractGenerated,
                    "Your booking has been confirmed",
                    $"Your booking with {vendor.BusinessName} for {booking.EventDate:d} has been confirmed. " +
                    "Your Event Booking Contract is now available to view or download.",
                    BuildDataJson(new
                    {
                        bookingId = booking.Id,
                        vendorName = vendor.BusinessName,
                        eventDate = booking.EventDate,
                        contractUrl
                    })));
            }

            await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
                vendorUserId,
                NotificationTypes.ContractGenerated,
                "Booking confirmed successfully",
                "Your Booking Contract has been generated.",
                BuildDataJson(new { bookingId = booking.Id, contractUrl })));
        }
        else
        {
            // Contract generation failed or was skipped - the parties still need to know the booking itself is confirmed.
            if (client is not null)
            {
                await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
                    client.UserId,
                    NotificationTypes.BookingAccepted,
                    "Booking request accepted",
                    $"Your booking request for {booking.EventDate:d} was accepted and your payment has been processed.",
                    BuildDataJson(new { bookingId = booking.Id })));
            }

            await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
                vendorUserId,
                NotificationTypes.BookingAccepted,
                "Booking confirmed successfully",
                $"The booking for {booking.EventDate:d} is confirmed.",
                BuildDataJson(new { bookingId = booking.Id })));
        }

        if (vendor is not null && vendorUser is not null && string.IsNullOrWhiteSpace(vendor.PartnershipAgreementUrl))
        {
            var agreementUrl = await GenerateVendorPartnershipBestEffortAsync(vendor, vendorUser, booking.Id);

            if (agreementUrl is not null)
            {
                await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
                    vendorUserId,
                    NotificationTypes.PartnershipAgreementGenerated,
                    "Partnership agreement generated",
                    "Your Partnership Agreement with Planura has been generated and is awaiting admin review.",
                    BuildDataJson(new { vendorId = vendor.Id, agreementUrl })));

                await NotifyBestEffortAsync(() => _notificationService.NotifyRoleAsync(
                    Roles.Admin,
                    NotificationTypes.PartnershipAgreementPendingReview,
                    "A new Vendor Partnership Agreement requires review",
                    $"{vendor.BusinessName} now has a new Partnership Agreement awaiting review.",
                    BuildDataJson(new
                    {
                        vendorId = vendor.Id,
                        vendorName = vendor.BusinessName,
                        bookingId = booking.Id,
                        generatedAt = vendor.PartnershipAgreementGeneratedAt,
                        agreementUrl
                    })));
            }
        }
    }

    /// <summary>Drafts and stores the Event Booking Contract PDF. Returns the absolute download URL, or null on any failure.</summary>
    private async Task<string?> GenerateBookingContractBestEffortAsync(
        BookingRequest booking, Vendor vendor, EventPlan eventPlan, Client client, ApplicationUser clientUser, ApplicationUser vendorUser)
    {
        if (booking.AgreedPrice is null or <= 0)
        {
            _logger.LogWarning("Skipping contract generation for booking request {BookingRequestId}: no agreed price on record.", booking.Id);
            return null;
        }

        try
        {
            var contractDto = new GenerateContractDto
            {
                ClientName = clientUser.FullName,
                ClientEmail = clientUser.Email,
                ClientPhone = clientUser.PhoneNumber,
                ClientAddress = client.City,
                ClientRepresentativeName = clientUser.FullName,
                VendorName = vendor.BusinessName,
                VendorEmail = vendorUser.Email,
                VendorPhone = vendorUser.PhoneNumber,
                VendorAddress = string.IsNullOrWhiteSpace(vendor.Address) ? vendor.City : vendor.Address,
                VendorRepresentativeName = vendorUser.FullName,
                EventType = eventPlan.EventType,
                EventDate = booking.EventDate,
                EventLocation = eventPlan.City,
                GuestCount = booking.GuestCount ?? eventPlan.GuestCount,
                Price = booking.AgreedPrice!.Value,
                Currency = _stripeOptions.DefaultCurrency,
                AdditionalTerms = booking.ClientMessage
            };

            var document = await _contractService.GenerateBookingContractAsync(contractDto);
            var relativeUrl = await _attachmentService.UploadGeneratedFileAsync(document.Content, document.FileName, BookingContractsFolder);

            booking.ContractId = document.ContractId;
            booking.ContractDocumentUrl = relativeUrl;
            booking.ContractGeneratedAt = DateTimeOffset.UtcNow;
            booking.UpdatedAt = DateTimeOffset.UtcNow;

            _unitOfWork.Repository<BookingRequest, long>().Update(booking);
            await _unitOfWork.SaveChangesAsync();

            return _attachmentService.ToAbsoluteUrl(relativeUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate the Event Booking Contract for booking request {BookingRequestId}.", booking.Id);
            return null;
        }
    }

    /// <summary>Drafts and stores the Vendor Partnership Agreement PDF. Returns the absolute download URL, or null on any failure.</summary>
    private async Task<string?> GenerateVendorPartnershipBestEffortAsync(Vendor vendor, ApplicationUser vendorUser, long triggeringBookingId)
    {
        try
        {
            string? categoryName = null;
            if (vendor.CategoryId is not null)
            {
                var category = await _unitOfWork.Repository<ServiceCategory, long>().GetAsync(vendor.CategoryId.Value);
                categoryName = category?.NameEn;
            }

            var partnershipDto = new GenerateVendorPartnershipDto
            {
                VendorName = vendor.BusinessName,
                VendorEmail = vendorUser.Email,
                VendorPhone = vendorUser.PhoneNumber,
                VendorAddress = string.IsNullOrWhiteSpace(vendor.Address) ? vendor.City : vendor.Address,
                VendorRepresentativeName = vendorUser.FullName,
                VendorCategory = categoryName,
                VendorCity = vendor.City,
                EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            var document = await _contractService.GenerateVendorPartnershipContractAsync(partnershipDto);
            var relativeUrl = await _attachmentService.UploadGeneratedFileAsync(document.Content, document.FileName, VendorPartnershipAgreementsFolder);

            vendor.PartnershipAgreementId = document.ContractId;
            vendor.PartnershipAgreementUrl = relativeUrl;
            vendor.PartnershipAgreementGeneratedAt = DateTimeOffset.UtcNow;
            vendor.UpdatedAt = DateTimeOffset.UtcNow;

            _unitOfWork.Repository<Vendor, long>().Update(vendor);
            await _unitOfWork.SaveChangesAsync();

            return _attachmentService.ToAbsoluteUrl(relativeUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to generate the Vendor Partnership Agreement for vendor {VendorId} (triggered by booking {BookingId}).",
                vendor.Id, triggeringBookingId);
            return null;
        }
    }

    private static string BuildDataJson(object payload) => JsonSerializer.Serialize(payload, NotificationDataJsonOptions);

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
