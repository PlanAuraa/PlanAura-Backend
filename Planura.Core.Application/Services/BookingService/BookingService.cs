using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Planura.Core.Application.Abstraction.AttachementService;
using Planura.Core.Application.Abstraction.BookingAgreement;
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
    private readonly IAgreementPreviewStore _agreementPreviewStore;
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
        IAgreementPreviewStore agreementPreviewStore,
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
        _agreementPreviewStore = agreementPreviewStore;
        _bookingOptions = bookingOptions.Value;
        _stripeOptions = stripeOptions.Value;
        _logger = logger;
    }

    public async Task<AgreementPreviewResultDto> PreviewBookingAgreementAsync(long clientUserId, AgreementPreviewRequestDto dto)
    {
        var facts = await ResolveBookingFactsAsync(
            clientUserId, dto.EventPlanId, dto.AvailabilityId, dto.VendorPackageId, dto.GuestCount);

        var clientUser = await _unitOfWork.Repository<ApplicationUser, long>().GetAsync(facts.Client.UserId);
        if (clientUser is null)
        {
            throw new NotFoundExeption(nameof(ApplicationUser), facts.Client.UserId);
        }

        var eventDate = DateOnly.FromDateTime(facts.Slot.StartAt.UtcDateTime);
        var contractDto = BuildBookingContractDto(facts, clientUser, dto.GuestCount, dto.ClientMessage, eventDate);

        var document = await _contractService.GenerateBookingContractAsync(contractDto);
        var relativeUrl = await _attachmentService.UploadGeneratedFileAsync(
            document.Content, document.FileName, BookingContractsFolder);
        if (relativeUrl is null)
        {
            throw new BadRequestExeption("The Booking Agreement could not be prepared. Please try again.");
        }

        var generatedAt = DateTimeOffset.UtcNow;
        var token = _agreementPreviewStore.Put(
            new AgreementPreviewEntry(facts.Client.Id, document.ContractId, relativeUrl, generatedAt));

        return new AgreementPreviewResultDto
        {
            Token = token,
            ContractId = document.ContractId,
            DocumentUrl = _attachmentService.ToAbsoluteUrl(relativeUrl)!,
            GeneratedAt = generatedAt
        };
    }

    public async Task<BookingRequestDto> CreateBookingRequestAsync(long clientUserId, CreateBookingRequestDto dto)
    {
        var facts = await ResolveBookingFactsAsync(
            clientUserId, dto.EventPlanId, dto.AvailabilityId, dto.VendorPackageId, dto.GuestCount);
        var slot = facts.Slot;
        var vendor = facts.Vendor;
        var agreedPrice = facts.AgreedPrice;

        if (string.IsNullOrWhiteSpace(dto.PaymentMethodId))
        {
            throw new BadRequestExeption("A payment method is required to submit a booking request.");
        }

        if (string.IsNullOrWhiteSpace(dto.RequestId))
        {
            throw new BadRequestExeption("A request id is required to submit a booking request.");
        }

        if (!dto.AgreementAccepted)
        {
            throw new BadRequestExeption("You must read and agree to the Booking Agreement before confirming.");
        }

        // Redeem the Booking Agreement the client reviewed at the payment step. Peek (not remove) so a
        // later failure — e.g. a declined card the client retries with another one — leaves the token
        // valid; it's removed only after the booking commits. Ownership is re-checked so a token can
        // only ever attach its own client's reviewed contract.
        var agreement = _agreementPreviewStore.TryGet(dto.AgreementToken);
        if (agreement is null || agreement.ClientId != facts.Client.Id)
        {
            throw new BadRequestExeption(
                "Your Booking Agreement has expired. Please review the agreement again before confirming.");
        }

        // Authorize the card hold before touching the database: if this fails (declined, insufficient
        // funds, etc.) no booking request or payment row is ever created — see the option (b) status
        // model discussion. The only failure window left is between this call succeeding and the
        // transaction below committing, which is handled with a compensating cancel in the catch blocks.
        var intentResult = await _paymentGatewayService.AuthorizePaymentIntentAsync(new AuthorizePaymentIntentRequest
        {
            AmountInSmallestUnit = StripeAmountConverter.ToSmallestUnit(agreedPrice),
            Currency = _stripeOptions.DefaultCurrency.ToLowerInvariant(),
            PaymentMethodId = dto.PaymentMethodId,
            IdempotencyKey = dto.RequestId,
            Metadata = new Dictionary<string, string>
            {
                ["client_id"] = facts.Client.Id.ToString(),
                ["vendor_id"] = vendor.Id.ToString(),
                ["event_plan_id"] = dto.EventPlanId.ToString(),
                ["availability_id"] = dto.AvailabilityId.ToString()
            }
        });

        var booking = new BookingRequest
        {
            EventPlanId = dto.EventPlanId,
            ClientId = facts.Client.Id,
            VendorId = vendor.Id,
            VendorPackageId = dto.VendorPackageId,
            EventDate = DateOnly.FromDateTime(slot.StartAt.UtcDateTime),
            GuestCount = dto.GuestCount,
            AgreedPrice = agreedPrice,
            ClientMessage = dto.ClientMessage,
            Status = BookingStatus.Pending,
            PaymentStatus = BookingPaymentStatus.Unpaid,
            // Bind the exact agreement the client reviewed (generated at the payment step) and stamp
            // their consent. The vendor later reviews this same stored contract before accepting.
            ContractId = agreement.ContractId,
            ContractDocumentUrl = agreement.RelativeUrl,
            ContractGeneratedAt = agreement.GeneratedAt,
            ClientAgreedAt = DateTimeOffset.UtcNow
        };

        var payment = new Payment
        {
            ClientId = facts.Client.Id,
            VendorId = vendor.Id,
            Amount = agreedPrice,
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
            _unitOfWork.Repository<VendorAvailability, long>().Update(slot);

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

        // Booking committed with its agreed contract attached — the token has served its purpose.
        _agreementPreviewStore.Remove(dto.AgreementToken);

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

    public async Task<BookingRequestDto> AcceptBookingRequestAsync(long bookingRequestId, long vendorUserId, bool agreementAccepted)
    {
        if (!agreementAccepted)
        {
            throw new BadRequestExeption("You must read and agree to the Booking Agreement before accepting.");
        }

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
        booking.VendorAgreedAt = DateTimeOffset.UtcNow;
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

        // Post-accept side effects: notify client/vendor about the (already-attached) Event Booking
        // Contract, and generate the vendor's one-time Partnership Agreement if they don't have one yet.
        // The booking contract itself was generated and agreed at the payment step, so nothing is drafted
        // here for it. All best-effort: payment is already captured, so a Gemini/PDF/notify hiccup must
        // never fail this call or undo the accepted booking.
        var client = await _unitOfWork.Repository<Client, long>().GetAsync(booking.ClientId);
        await ProcessAcceptedBookingAsync(booking, client, vendorUserId);

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

        var booking = await _unitOfWork.Repository<BookingRequest, long>()
            .GetWithSpecAsync(new BookingRequestByIdSpecification(bookingRequestId));
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
            new BookingRequestsByVendorSpecification(
                vendorId, filter.Status, filter.PaymentStatus, filter.ExcludeRefunded, skip: null, take: null));

        var items = await repo.GetAllWithSpecAsync(
            new BookingRequestsByVendorSpecification(
                vendorId, filter.Status, filter.PaymentStatus, filter.ExcludeRefunded, skip, pageSize));

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

        var booking = await _unitOfWork.Repository<BookingRequest, long>()
            .GetWithSpecAsync(new BookingRequestByIdSpecification(bookingRequestId));
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

        var repo = _unitOfWork.Repository<BookingRequest, long>();
        var booking = await repo.GetAsync(bookingRequestId);
        if (booking is null || !await IsBookingParticipantAsync(booking, userId))
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
            Notes = $"{BookingStatusHistoryNotes.DisputeRaisedPrefix}{reason}"
        };
        await _unitOfWork.Repository<BookingStatusHistory, long>().AddAsync(history);

        await _unitOfWork.SaveChangesAsync();

        return MapBooking(booking);
    }

    /// <summary>
    /// Either side of a booking may raise a dispute, so this accepts the booking's client *or* its
    /// vendor. The client is checked first and short-circuits, since that is the common case and it
    /// avoids a second lookup. Callers treat a false result as "not found" rather than "forbidden",
    /// so a user cannot probe for booking ids that aren't theirs.
    /// </summary>
    private async Task<bool> IsBookingParticipantAsync(BookingRequest booking, long userId)
    {
        var client = await _unitOfWork.Repository<Client, long>()
            .GetWithSpecAsync(new ClientByUserIdSpecification(userId));
        if (client is not null && booking.ClientId == client.Id)
        {
            return true;
        }

        var vendor = await _unitOfWork.Repository<Vendor, long>()
            .GetWithSpecAsync(new VendorByUserIdSpecification(userId));
        return vendor is not null && booking.VendorId == vendor.Id;
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

    /// <summary>The fully-resolved, validated parties and slot behind a booking — shared by the agreement
    /// preview and the actual booking creation so both apply identical rules and see the same facts.</summary>
    private sealed record BookingFacts(
        Client Client,
        Vendor Vendor,
        ApplicationUser VendorUser,
        EventPlan EventPlan,
        VendorAvailability Slot,
        decimal AgreedPrice);

    /// <summary>
    /// Resolves and validates every party/slot/package fact needed to draft the Booking Agreement and,
    /// later, to create the booking: client ownership of the event plan, the slot being genuinely
    /// available, the vendor verified and active, the package belonging to that vendor, guest count
    /// within the package limit, and a positive agreed price (the package's, the server's source of
    /// truth). Throws the same domain exceptions the create path always has.
    /// </summary>
    private async Task<BookingFacts> ResolveBookingFactsAsync(
        long clientUserId, long eventPlanId, long availabilityId, long? vendorPackageId, int? guestCount)
    {
        var client = await _unitOfWork.Repository<Client, long>()
            .GetWithSpecAsync(new ClientByUserIdSpecification(clientUserId));
        if (client is null)
        {
            throw new NotFoundExeption(nameof(Client), clientUserId);
        }

        var eventPlan = await _unitOfWork.Repository<EventPlan, long>().GetAsync(eventPlanId);
        if (eventPlan is null || eventPlan.ClientId != client.Id)
        {
            throw new NotFoundExeption(nameof(EventPlan), eventPlanId);
        }

        var slot = await _unitOfWork.Repository<VendorAvailability, long>()
            .GetWithSpecAsync(new VendorAvailabilityByIdSpecification(availabilityId));
        if (slot is null)
        {
            throw new NotFoundExeption(nameof(VendorAvailability), availabilityId);
        }

        if (slot.Status != AvailabilityStatus.Available)
        {
            if (slot.BookingRequest is not null && slot.BookingRequest.ClientId == client.Id)
            {
                throw new SlotUnavailableExeption("You already have a booking for this exact time slot.");
            }

            throw new SlotUnavailableExeption($"Slot {availabilityId} is no longer available for booking.");
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

        if (vendorPackageId is null)
        {
            throw new BadRequestExeption("A package must be selected so a price can be authorized for this booking.");
        }

        var package = await _unitOfWork.Repository<VendorPackage, long>().GetAsync(vendorPackageId.Value);
        if (package is null || package.VendorId != vendor.Id)
        {
            throw new NotFoundExeption(nameof(VendorPackage), vendorPackageId.Value);
        }

        if (package.MaxGuests is not null && guestCount is not null && guestCount > package.MaxGuests)
        {
            throw new BadRequestExeption(
                $"Guest count exceeds the package maximum of {package.MaxGuests} guests.");
        }

        if (package.BasePrice <= 0)
        {
            throw new BadRequestExeption("A package must be selected so a price can be authorized for this booking.");
        }

        return new BookingFacts(client, vendor, vendorUser, eventPlan, slot, package.BasePrice);
    }

    /// <summary>Builds the Event Booking Contract input from resolved facts plus the client's account
    /// (loaded only for the preview, since booking creation doesn't need it). Shared so the agreement the
    /// client previews is drafted from exactly the same fields the accepted contract would have been.</summary>
    private GenerateContractDto BuildBookingContractDto(
        BookingFacts facts, ApplicationUser clientUser, int? guestCount, string? clientMessage, DateOnly eventDate)
    {
        return new GenerateContractDto
        {
            ClientName = clientUser.FullName,
            ClientEmail = clientUser.Email,
            ClientPhone = clientUser.PhoneNumber,
            ClientAddress = facts.Client.City,
            ClientRepresentativeName = clientUser.FullName,
            VendorName = facts.Vendor.BusinessName,
            VendorEmail = facts.VendorUser.Email,
            VendorPhone = facts.VendorUser.PhoneNumber,
            VendorAddress = string.IsNullOrWhiteSpace(facts.Vendor.Address) ? facts.Vendor.City : facts.Vendor.Address,
            VendorRepresentativeName = facts.VendorUser.FullName,
            EventType = facts.EventPlan.EventType,
            EventDate = eventDate,
            EventLocation = facts.EventPlan.City,
            GuestCount = guestCount ?? facts.EventPlan.GuestCount,
            Price = facts.AgreedPrice,
            Currency = _stripeOptions.DefaultCurrency,
            AdditionalTerms = clientMessage
        };
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
    /// Side effects that follow a vendor accepting (confirming) a booking: notify the client and vendor
    /// that the booking is confirmed (surfacing the Event Booking Contract that was generated and agreed
    /// at the payment step and is already attached to the booking), and draft + store the Vendor/Planura
    /// Partnership Agreement exactly once per vendor. Every step here is best-effort — the booking is
    /// already Accepted/Paid by the time this runs, so nothing in here is allowed to throw to the caller.
    /// </summary>
    private async Task ProcessAcceptedBookingAsync(BookingRequest booking, Client? client, long vendorUserId)
    {
        var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(booking.VendorId);
        if (vendor is null)
        {
            return;
        }

        var vendorUser = await _unitOfWork.Repository<ApplicationUser, long>().GetAsync(vendor.UserId);

        // The Event Booking Contract was drafted and agreed at the payment step and copied onto the
        // booking at creation, so it's simply surfaced here — never regenerated on accept.
        var contractUrl = _attachmentService.ToAbsoluteUrl(booking.ContractDocumentUrl);

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
