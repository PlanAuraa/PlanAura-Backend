using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Planura.Core.Application.Abstraction.AttachementService;
using Planura.Core.Application.Abstraction.BookingAgreement;
using Planura.Core.Application.Abstraction.PaymentGateway;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models;
using Planura.Core.Application.Models.AdminBooking;
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

        // Full-vs-deposit decision (Phase 1). Both the total and the deposit are derived server-side
        // from the package price (agreedPrice) — never from client input. Events close to now take the
        // full-payment path (today's exact behavior); events further out authorize only the deposit.
        var eventDate = DateOnly.FromDateTime(slot.StartAt.UtcDateTime);
        var plan = ResolvePaymentPlan(agreedPrice, eventDate);

        // Deposit path (Phase 2): ensure the client has a Stripe Customer so the card can be saved and
        // charged off-session for the remainder later. Created lazily on the first deposit booking and
        // reused thereafter. A newly created id is persisted onto the client inside the transaction below,
        // so a customer created here but then abandoned (declined auth / rolled-back write) is at worst a
        // harmless unused Stripe Customer. The full-payment path never needs this.
        string? customerId = plan.IsDeposit ? facts.Client.StripeCustomerId : null;
        var createdNewCustomer = false;
        if (plan.IsDeposit && string.IsNullOrWhiteSpace(customerId))
        {
            var clientUser = await _unitOfWork.Repository<ApplicationUser, long>().GetAsync(facts.Client.UserId);
            customerId = await _paymentGatewayService.CreateCustomerAsync(new CreateCustomerRequest
            {
                Email = clientUser?.Email,
                Name = clientUser?.FullName,
                IdempotencyKey = $"customer-client-{facts.Client.Id}",
                Metadata = new Dictionary<string, string> { ["client_id"] = facts.Client.Id.ToString() }
            });
            createdNewCustomer = true;
        }

        // Authorize the card hold before touching the database: if this fails (declined, insufficient
        // funds, etc.) no booking request or payment row is ever created — see the option (b) status
        // model discussion. The only failure window left is between this call succeeding and the
        // transaction below committing, which is handled with a compensating cancel in the catch blocks.
        var intentResult = await _paymentGatewayService.AuthorizePaymentIntentAsync(new AuthorizePaymentIntentRequest
        {
            AmountInSmallestUnit = StripeAmountConverter.ToSmallestUnit(plan.AuthorizeAmount),
            Currency = _stripeOptions.DefaultCurrency.ToLowerInvariant(),
            PaymentMethodId = dto.PaymentMethodId,
            IdempotencyKey = dto.RequestId,
            // Deposit path attaches to the Customer and saves the card off-session; full path does neither.
            CustomerId = customerId,
            SaveCardForOffSession = plan.IsDeposit,
            Metadata = new Dictionary<string, string>
            {
                ["client_id"] = facts.Client.Id.ToString(),
                ["vendor_id"] = vendor.Id.ToString(),
                ["event_plan_id"] = dto.EventPlanId.ToString(),
                ["availability_id"] = dto.AvailabilityId.ToString(),
                ["is_deposit"] = plan.IsDeposit ? "true" : "false",
                ["deposit_amount"] = plan.DepositAmount.ToString(),
                ["total_amount"] = plan.TotalAmount.ToString()
            }
        });

        var booking = new BookingRequest
        {
            EventPlanId = dto.EventPlanId,
            ClientId = facts.Client.Id,
            VendorId = vendor.Id,
            VendorPackageId = dto.VendorPackageId,
            EventDate = eventDate,
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
            // Amount is what was actually authorized/held: the deposit on the deposit path, the full
            // price on the full path. TotalAmount always carries the full price so the outstanding
            // remainder is derivable later.
            Amount = plan.AuthorizeAmount,
            IsDeposit = plan.IsDeposit,
            DepositAmount = plan.IsDeposit ? plan.DepositAmount : null,
            TotalAmount = plan.TotalAmount,
            Status = plan.IsDeposit ? PaymentStatus.DepositAuthorized : PaymentStatus.Authorized,
            PaymentMethod = dto.PaymentMethodId,
            // Deposit path saves the card for the later off-session remainder charge; full path leaves this null.
            SavedPaymentMethodId = plan.IsDeposit ? dto.PaymentMethodId : null,
            GatewayReference = intentResult.PaymentIntentId,
            AuthorizedAt = DateTimeOffset.UtcNow,
            BookingRequest = booking
        };

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _unitOfWork.Repository<BookingRequest, long>().AddAsync(booking);
            await _unitOfWork.Repository<Payment, long>().AddAsync(payment);

            // Persist a newly created Stripe Customer id onto the client so it's reused on future deposit
            // bookings (and available to the remainder-charge job). Only when we created one this call.
            if (createdNewCustomer)
            {
                facts.Client.StripeCustomerId = customerId;
                facts.Client.UpdatedAt = DateTimeOffset.UtcNow;
                _unitOfWork.Repository<Client, long>().Update(facts.Client);
            }

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

    /// <summary>
    /// The client confirms the service was actually delivered — the only client-driven way an
    /// AwaitingConfirmation booking reaches Completed (the other is BookingAutoCompleteJob's
    /// auto-confirm pass, once the grace window elapses with no open dispute). "Report a problem"
    /// instead uses the existing FlagDisputeAsync, which now accepts AwaitingConfirmation too.
    /// </summary>
    public async Task<BookingRequestDto> ConfirmServiceDeliveredAsync(long bookingRequestId, long clientUserId)
    {
        var clientId = await ResolveClientIdAsync(clientUserId);

        var repo = _unitOfWork.Repository<BookingRequest, long>();
        var booking = await repo.GetAsync(bookingRequestId);
        if (booking is null || booking.ClientId != clientId)
        {
            throw new NotFoundExeption(nameof(BookingRequest), bookingRequestId);
        }

        if (booking.Status != BookingStatus.AwaitingConfirmation)
        {
            throw new BadRequestExeption(
                $"Cannot confirm a booking request with status '{booking.Status}'. Only bookings awaiting confirmation can be confirmed.");
        }

        if (booking.DisputeStatus == DisputeStatus.Open)
        {
            throw new BadRequestExeption(
                "This booking has an open dispute — it will be resolved by an admin instead of a direct confirmation.");
        }

        var now = DateTimeOffset.UtcNow;
        booking.Status = BookingStatus.Completed;
        booking.CompletedAt = now;
        booking.UpdatedAt = now;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            repo.Update(booking);

            // Mirrors BookingAutoCompleteJob's auto-confirm pass: same one-time, exact-once
            // increment, just triggered by the client instead of the grace-window timer.
            var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(booking.VendorId);
            if (vendor is not null)
            {
                vendor.TotalCompletedBookings += 1;
                vendor.UpdatedAt = now;
                _unitOfWork.Repository<Vendor, long>().Update(vendor);
            }

            var history = new BookingStatusHistory
            {
                BookingRequestId = bookingRequestId,
                PreviousStatus = BookingStatus.AwaitingConfirmation.ToString(),
                NewStatus = BookingStatus.Completed.ToString(),
                ChangedByUserId = clientUserId,
                Notes = "Confirmed by client: service delivered."
            };
            await _unitOfWork.Repository<BookingStatusHistory, long>().AddAsync(history);

            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }

        var confirmedVendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(booking.VendorId);
        if (confirmedVendor is not null)
        {
            await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
                confirmedVendor.UserId,
                NotificationTypes.BookingCompleted,
                "Booking completed",
                $"The client confirmed your booking for {booking.EventDate:d} is complete."));
        }

        return await MapBookingWithSlotAsync(booking);
    }

    /// <summary>Previews the refund the client would receive if they requested cancellation right
    /// now, without changing anything — lets the UI show the estimate before they commit.</summary>
    public async Task<CancellationQuoteDto> GetCancellationQuoteAsync(long bookingRequestId, long clientUserId)
    {
        var clientId = await ResolveClientIdAsync(clientUserId);

        var booking = await _unitOfWork.Repository<BookingRequest, long>().GetAsync(bookingRequestId);
        if (booking is null || booking.ClientId != clientId)
        {
            throw new NotFoundExeption(nameof(BookingRequest), bookingRequestId);
        }

        if (booking.Status != BookingStatus.Accepted)
        {
            throw new BadRequestExeption(
                $"Cannot quote a cancellation for a booking request with status '{booking.Status}'. Only accepted bookings can be cancelled.");
        }

        var quotePayment = await _unitOfWork.Repository<Payment, long>()
            .GetWithSpecAsync(new DepositPaymentByBookingRequestSpecification(bookingRequestId));
        if (IsDepositOnly(quotePayment))
        {
            // Deposit-only booking (remainder not paid): cancelling forfeits the deposit — non-refundable.
            var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
            return new CancellationQuoteDto
            {
                DaysUntilEvent = booking.EventDate.DayNumber - today.DayNumber,
                RefundPercent = 0m,
                RefundAmount = 0m
            };
        }

        var (percent, amount, daysUntilEvent) = ResolveCancellationRefund(booking);
        return new CancellationQuoteDto
        {
            DaysUntilEvent = daysUntilEvent,
            RefundPercent = percent,
            RefundAmount = amount
        };
    }

    /// <summary>
    /// A deposit-path booking whose remainder has NOT been collected (still owing it — DepositPaid_RemainderDue
    /// or RemainderFailed). Cancelling such a booking forfeits the deposit: immediate cancel, slot released, no
    /// refund and no admin review — mirroring the grace-expiry job's forfeit outcome. A fully-paid deposit
    /// (FullyPaid) and a full-payment booking (Completed) are NOT deposit-only and keep the admin-review path.
    /// </summary>
    private static bool IsDepositOnly(Payment? payment) =>
        payment is not null
        && payment.IsDeposit
        && payment.Status is PaymentStatus.DepositPaid_RemainderDue or PaymentStatus.RemainderFailed;

    /// <summary>
    /// The client requests cancellation of an Accepted booking. This does NOT cancel the booking,
    /// release the vendor's slot, or create a refund — it moves the booking to
    /// CancellationRequested and computes a refund estimate (locked in now, so admin-review delay
    /// doesn't cost the client), then waits for an admin to approve or reject
    /// (AdminBookingService.ApproveCancellationAsync / RejectCancellationAsync). Post-service issues
    /// go through FlagDisputeAsync instead, not this.
    /// </summary>
    public async Task<BookingRequestDto> RequestCancellationAsync(long bookingRequestId, long clientUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BadRequestExeption("A cancellation reason is required.");
        }

        var clientId = await ResolveClientIdAsync(clientUserId);

        var repo = _unitOfWork.Repository<BookingRequest, long>();
        var booking = await repo.GetAsync(bookingRequestId);
        if (booking is null || booking.ClientId != clientId)
        {
            throw new NotFoundExeption(nameof(BookingRequest), bookingRequestId);
        }

        if (booking.Status != BookingStatus.Accepted)
        {
            throw new BadRequestExeption(
                $"Cannot request cancellation for a booking request with status '{booking.Status}'. Only accepted bookings can be cancelled.");
        }

        var payment = await _unitOfWork.Repository<Payment, long>()
            .GetWithSpecAsync(new DepositPaymentByBookingRequestSpecification(bookingRequestId));

        // A remainder charge is mid-flight (e.g. the client is completing SCA) — don't cancel underneath it.
        if (payment is not null && payment.IsDeposit && payment.Status == PaymentStatus.RemainderCharging)
        {
            throw new BadRequestExeption(
                "A payment for the remaining balance is being processed. Please wait a moment before cancelling.");
        }

        // Deposit-only (remainder unpaid): forfeit the deposit and cancel immediately — no admin review.
        if (IsDepositOnly(payment))
        {
            return await CancelDepositOnlyImmediatelyAsync(booking, clientUserId, reason);
        }

        // Fully-paid deposit (FullyPaid) and full-payment (Completed) bookings: unchanged admin-review path.
        var (percent, amount, _) = ResolveCancellationRefund(booking);

        var now = DateTimeOffset.UtcNow;
        booking.Status = BookingStatus.CancellationRequested;
        booking.CancellationReason = reason;
        booking.CancellationRequestedAt = now;
        booking.CancellationRefundPercent = percent;
        booking.CancellationRefundAmount = amount;
        booking.RefundStatus = RefundStatus.PendingReview;
        booking.UpdatedAt = now;

        // The vendor's slot deliberately stays Booked and no refund is created here — both wait for
        // the admin's decision (see ApproveCancellationAsync/RejectCancellationAsync).
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            repo.Update(booking);

            var history = new BookingStatusHistory
            {
                BookingRequestId = bookingRequestId,
                PreviousStatus = BookingStatus.Accepted.ToString(),
                NewStatus = BookingStatus.CancellationRequested.ToString(),
                ChangedByUserId = clientUserId,
                Notes = $"Cancellation requested by client (estimated refund {percent:0.##}% = {amount:0.00}): {reason}"
            };
            await _unitOfWork.Repository<BookingStatusHistory, long>().AddAsync(history);

            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }

        await NotifyBestEffortAsync(() => _notificationService.NotifyRoleAsync(
            Roles.Admin,
            NotificationTypes.BookingCancellationRequested,
            "A booking cancellation needs review",
            $"A client requested cancellation of the booking for {booking.EventDate:d} " +
            $"(estimated refund {percent:0.##}%). Please review.",
            BuildDataJson(new { bookingId = booking.Id })));

        var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(booking.VendorId);
        if (vendor is not null)
        {
            await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
                vendor.UserId,
                NotificationTypes.BookingCancellationRequested,
                "Client requested a cancellation",
                $"The client requested to cancel the booking for {booking.EventDate:d}. " +
                "Your slot stays reserved until an admin reviews the request."));
        }

        return await MapBookingWithSlotAsync(booking);
    }

    /// <summary>
    /// Deposit-only cancellation: the client cancels a booking whose remainder was never paid. The deposit is
    /// forfeited (kept — no Stripe refund), the booking is cancelled immediately and the vendor's slot is
    /// released, with NO admin review entry. This mirrors the grace-expiry job's forfeit outcome, just
    /// client-initiated and resolved at once. Both background jobs skip it afterwards (they select on
    /// Status == Accepted, and this booking is now Cancelled).
    /// </summary>
    private async Task<BookingRequestDto> CancelDepositOnlyImmediatelyAsync(BookingRequest booking, long clientUserId, string reason)
    {
        var now = DateTimeOffset.UtcNow;
        var previousStatus = booking.Status;
        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = now;
        booking.CancellationReason = reason;
        booking.CancellationRequestedAt = now;
        booking.CancellationReviewedAt = now;      // resolved immediately — there is no admin step
        booking.CancellationRefundPercent = 0m;
        booking.CancellationRefundAmount = 0m;
        booking.RefundStatus = RefundStatus.None;  // deposit forfeited — nothing is refunded
        booking.UpdatedAt = now;

        var repo = _unitOfWork.Repository<BookingRequest, long>();
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            repo.Update(booking);

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
                NewStatus = BookingStatus.Cancelled.ToString(),
                ChangedByUserId = clientUserId,
                Notes = "Cancelled by client; remaining balance unpaid, so the deposit is forfeited (non-refundable)."
            };
            await _unitOfWork.Repository<BookingStatusHistory, long>().AddAsync(history);

            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }

        // No admin notification — there is no review. Just let the vendor know the slot is free again.
        var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(booking.VendorId);
        if (vendor is not null)
        {
            await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
                vendor.UserId,
                NotificationTypes.BookingCancelled,
                "Booking cancelled",
                $"The client cancelled their booking for {booking.EventDate:d}. Your slot is available again."));
        }

        return await MapBookingWithSlotAsync(booking);
    }

    /// <summary>Days-remaining-before-event refund tiers (BookingOptions.CancellationTiers): the tier
    /// with the highest MinDaysBefore that daysUntilEvent still satisfies applies. Tiers are sorted
    /// descending here regardless of config order, so a misordered appsettings list can't pick the
    /// wrong (too-generous) tier.</summary>
    private (decimal Percent, decimal Amount, int DaysUntilEvent) ResolveCancellationRefund(BookingRequest booking)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var daysUntilEvent = booking.EventDate.DayNumber - today.DayNumber;

        var tier = _bookingOptions.CancellationTiers
            .OrderByDescending(t => t.MinDaysBefore)
            .FirstOrDefault(t => daysUntilEvent >= t.MinDaysBefore);

        var percent = tier?.RefundPercent ?? 0m;
        var capturedAmount = booking.AgreedPrice ?? 0m;
        var amount = Math.Round(capturedAmount * (percent / 100m), 2, MidpointRounding.AwayFromZero);

        return (percent, amount, daysUntilEvent);
    }

    /// <summary>The full-vs-deposit split for a booking, all derived server-side from the package price.</summary>
    private readonly record struct PaymentPlan(
        bool IsDeposit, decimal AuthorizeAmount, decimal DepositAmount, decimal TotalAmount);

    /// <summary>
    /// Decides, at booking time, whether the client pays in full now or only a deposit, based on how
    /// far away the event is. Events within <see cref="BookingOptions.FullPaymentThresholdDays"/> days
    /// (inclusive) take the full-payment path — byte-for-byte today's behavior. Further out, only
    /// <see cref="BookingOptions.DepositPercentage"/>% of the price is authorized. The days-until-event
    /// calculation mirrors ResolveCancellationRefund (whole-day difference from UtcNow).
    /// </summary>
    private PaymentPlan ResolvePaymentPlan(decimal totalPrice, DateOnly eventDate)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var daysUntilEvent = eventDate.DayNumber - today.DayNumber;

        var deposit = Math.Round(
            totalPrice * (_bookingOptions.DepositPercentage / 100m), 2, MidpointRounding.AwayFromZero);

        var isDeposit = daysUntilEvent > _bookingOptions.FullPaymentThresholdDays;
        var authorizeAmount = isDeposit ? deposit : totalPrice;

        return new PaymentPlan(isDeposit, authorizeAmount, deposit, totalPrice);
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
            // Captures the full authorized hold. On the deposit path only the deposit was authorized,
            // so this captures exactly the deposit — the remainder is NOT collected here (Phase 1 has no
            // remainder-charge mechanism yet). On the full path this captures the full price, as today.
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

        // Deposit path rests at "deposit paid, remainder due" — the deposit is captured but the booking
        // is not fully paid (no remainder mechanism in Phase 1). Full path completes exactly as today.
        var isDepositPath = payment.IsDeposit;

        var previousStatus = booking.Status;
        booking.Status = BookingStatus.Accepted;
        booking.PaymentStatus = isDepositPath ? BookingPaymentStatus.DepositPaid : BookingPaymentStatus.Paid;
        booking.RespondedAt = DateTimeOffset.UtcNow;
        booking.VendorAgreedAt = DateTimeOffset.UtcNow;
        booking.UpdatedAt = DateTimeOffset.UtcNow;

        payment.Status = isDepositPath ? PaymentStatus.DepositPaid_RemainderDue : PaymentStatus.Completed;
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
                Notes = isDepositPath
                    ? "Accepted by vendor; deposit captured, remainder due."
                    : "Accepted by vendor; payment captured."
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

    /// <summary>
    /// Client pays the outstanding remainder on their deposit booking on-session (Phase 3). Eligible from
    /// DepositPaid_RemainderDue (before the auto-charge job runs) or RemainderFailed (after a failed
    /// auto-charge). Shares the background job's no-double-charge guard via an atomic claim: exactly one of
    /// {this call, the job} ever transitions the payment into RemainderCharging and charges it. Supports SCA
    /// — on requires_action the frontend completes authentication and the webhook finalizes the payment.
    /// </summary>
    public async Task<PayRemainderResultDto> PayRemainderAsync(long bookingRequestId, long clientUserId)
    {
        var client = await _unitOfWork.Repository<Client, long>()
            .GetWithSpecAsync(new ClientByUserIdSpecification(clientUserId));
        if (client is null)
        {
            throw new NotFoundExeption(nameof(Client), clientUserId);
        }

        var booking = await _unitOfWork.Repository<BookingRequest, long>().GetAsync(bookingRequestId);
        if (booking is null || booking.ClientId != client.Id)
        {
            // Ownership failures surface as NotFound so a client can't probe others' booking ids.
            throw new NotFoundExeption(nameof(BookingRequest), bookingRequestId);
        }

        var payment = await _unitOfWork.Repository<Payment, long>()
            .GetWithSpecAsync(new DepositPaymentByBookingRequestSpecification(bookingRequestId));
        if (payment is null || !payment.IsDeposit)
        {
            // Full-payment-path bookings have no remainder to pay.
            throw new BadRequestExeption("This booking has no remaining balance to pay — it was paid in full up front.");
        }

        if (payment.Status is not (PaymentStatus.DepositPaid_RemainderDue or PaymentStatus.RemainderFailed))
        {
            throw new BadRequestExeption(
                $"The remaining balance can't be paid from the current payment state ('{payment.Status}').");
        }

        if (string.IsNullOrWhiteSpace(payment.SavedPaymentMethodId) || string.IsNullOrWhiteSpace(client.StripeCustomerId))
        {
            throw new BadRequestExeption("No saved card is on file for this booking, so the remaining balance can't be charged.");
        }

        var remainder = (payment.TotalAmount ?? 0m) - (payment.DepositAmount ?? 0m);
        if (remainder <= 0m)
        {
            throw new BadRequestExeption("There is no remaining balance to charge on this booking.");
        }

        // Atomic claim shared with the background remainder-charge job (see IUnitOfWork.TryClaimRemainderChargeAsync).
        // If we can't claim, the job is charging it right now, an earlier SCA attempt is still in flight, or it
        // was just paid — in every case we must NOT charge again.
        var claimed = await _unitOfWork.TryClaimRemainderChargeAsync(payment.Id);
        if (!claimed)
        {
            throw new BadRequestExeption(
                "A payment for the remaining balance is already being processed for this booking. Please wait a moment and refresh.");
        }
        // Reflect the claim on the tracked entity so later Update()s don't null out what the claim UPDATE set.
        payment.Status = PaymentStatus.RemainderCharging;
        payment.RemainderChargingSince = DateTimeOffset.UtcNow;

        PaymentIntentResult result;
        try
        {
            result = await _paymentGatewayService.ChargeOnSessionAsync(new ChargeOnSessionRequest
            {
                AmountInSmallestUnit = StripeAmountConverter.ToSmallestUnit(remainder),
                Currency = _stripeOptions.DefaultCurrency.ToLowerInvariant(),
                CustomerId = client.StripeCustomerId!,
                PaymentMethodId = payment.SavedPaymentMethodId!,
                // Distinct from the job's off-session key; the claim (not the key) is the cross-actor guard.
                IdempotencyKey = $"remainder-onsession-{payment.Id}",
                Metadata = new Dictionary<string, string>
                {
                    ["booking_id"] = booking.Id.ToString(),
                    ["payment_id"] = payment.Id.ToString(),
                    ["kind"] = "remainder",
                    ["channel"] = "on_session"
                }
            });
        }
        catch (Exception)
        {
            // Hard decline: release the claim into RemainderFailed (grace clock starts) and surface the error.
            await MarkRemainderFailedFromClaimAsync(booking, payment, "The on-session remainder payment was declined.");
            throw;
        }

        if (result.Status == "succeeded")
        {
            await RecordRemainderPaidFromClaimAsync(booking, payment, result.PaymentIntentId, remainder);
            await NotifyRemainderFullyPaidBestEffortAsync(booking, client.UserId);
            return new PayRemainderResultDto
            {
                Status = result.Status,
                PaymentIntentId = result.PaymentIntentId,
                RequiresAction = false
            };
        }

        // SCA required: store the remainder PI id so the webhook can finalize once the client authenticates
        // in the browser; the payment stays RemainderCharging until then.
        await StoreRemainderPendingActionAsync(payment, result.PaymentIntentId);
        return new PayRemainderResultDto
        {
            Status = result.Status,
            PaymentIntentId = result.PaymentIntentId,
            ClientSecret = result.ClientSecret,
            RequiresAction = true
        };
    }

    private async Task RecordRemainderPaidFromClaimAsync(BookingRequest booking, Payment payment, string gatewayReference, decimal remainder)
    {
        var now = DateTimeOffset.UtcNow;
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            payment.Status = PaymentStatus.FullyPaid;
            payment.RemainderGatewayReference = gatewayReference;
            payment.RemainderChargedAt = now;
            payment.RemainderFailedAt = null;       // ends the grace window
            payment.RemainderFailureReason = null;
            _unitOfWork.Repository<Payment, long>().Update(payment);

            booking.PaymentStatus = BookingPaymentStatus.Paid;
            booking.UpdatedAt = now;
            _unitOfWork.Repository<BookingRequest, long>().Update(booking);

            var history = new BookingStatusHistory
            {
                BookingRequestId = booking.Id,
                PreviousStatus = booking.Status.ToString(),
                NewStatus = booking.Status.ToString(),
                ChangedByUserId = booking.Client?.UserId,
                Notes = $"remaining balance paid by client on-session ({remainder:0.00}); booking is now fully paid"
            };
            await _unitOfWork.Repository<BookingStatusHistory, long>().AddAsync(history);

            await _unitOfWork.CommitTransactionAsync();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogCritical(ex,
                "REMAINDER RECONCILIATION NEEDED: on-session charge {GatewayReference} for booking {BookingId} / " +
                "payment {PaymentId} succeeded at Stripe but recording FullyPaid failed and rolled back. " +
                "The payment stays RemainderCharging; verify the payment_intent.succeeded webhook reconciles it.",
                gatewayReference, booking.Id, payment.Id);
            throw;
        }
    }

    private async Task MarkRemainderFailedFromClaimAsync(BookingRequest booking, Payment payment, string reason)
    {
        var now = DateTimeOffset.UtcNow;
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            payment.Status = PaymentStatus.RemainderFailed;
            payment.RemainderFailureReason = reason.Length > 500 ? reason[..500] : reason;
            payment.RemainderFailedAt = now; // (re)start the grace clock
            _unitOfWork.Repository<Payment, long>().Update(payment);

            booking.PaymentStatus = BookingPaymentStatus.RemainderFailed;
            booking.UpdatedAt = now;
            _unitOfWork.Repository<BookingRequest, long>().Update(booking);

            var history = new BookingStatusHistory
            {
                BookingRequestId = booking.Id,
                PreviousStatus = booking.Status.ToString(),
                NewStatus = booking.Status.ToString(),
                ChangedByUserId = booking.Client?.UserId,
                Notes = "on-session remainder payment was declined; booking is awaiting resolution"
            };
            await _unitOfWork.Repository<BookingStatusHistory, long>().AddAsync(history);

            await _unitOfWork.CommitTransactionAsync();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex,
                "Failed to record RemainderFailed after a declined on-session payment for booking {BookingId} / payment {PaymentId}.",
                booking.Id, payment.Id);
        }
    }

    /// <summary>SCA path: persist the remainder PI id (so the webhook can match it later) while the payment
    /// stays RemainderCharging awaiting the client's browser-side authentication.</summary>
    private async Task StoreRemainderPendingActionAsync(Payment payment, string gatewayReference)
    {
        payment.RemainderGatewayReference = gatewayReference;
        _unitOfWork.Repository<Payment, long>().Update(payment);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task NotifyRemainderFullyPaidBestEffortAsync(BookingRequest booking, long clientUserId)
    {
        await NotifyBestEffortAsync(() => _notificationService.NotifyUserWithEmailAsync(
            clientUserId,
            NotificationTypes.RemainderPaid,
            "Your booking is now fully paid",
            $"Your payment for the remaining balance on your booking for {booking.EventDate:d} was successful. " +
            "Your booking is fully paid.",
            emailSubject: "Your booking is fully paid",
            emailBody: $"Your payment for the remaining balance on your booking for {booking.EventDate:d} was successful. " +
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
    /// The persistent, permanent record of everything that has happened to a booking — created,
    /// accepted, cancellation requested/approved/rejected, disputed/resolved, completed, etc. This is
    /// the source of truth the client-facing "Booking Activity" panel reads, so an outcome is never
    /// visible only through a transient notification.
    /// </summary>
    public async Task<List<BookingStatusHistoryEntryDto>> GetBookingTimelineAsync(long bookingRequestId, long clientUserId)
    {
        var clientId = await ResolveClientIdAsync(clientUserId);

        var booking = await _unitOfWork.Repository<BookingRequest, long>().GetAsync(bookingRequestId);
        if (booking is null || booking.ClientId != clientId)
        {
            throw new NotFoundExeption(nameof(BookingRequest), bookingRequestId);
        }

        return await LoadTimelineAsync(bookingRequestId);
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

    /// <summary>Vendor-side counterpart of GetBookingTimelineAsync — same permanent audit trail, scoped
    /// to bookings this vendor owns.</summary>
    public async Task<List<BookingStatusHistoryEntryDto>> GetVendorBookingTimelineAsync(long bookingRequestId, long vendorUserId)
    {
        var vendorId = await ResolveVendorIdAsync(vendorUserId);

        var booking = await _unitOfWork.Repository<BookingRequest, long>().GetAsync(bookingRequestId);
        if (booking is null || booking.VendorId != vendorId)
        {
            throw new NotFoundExeption(nameof(BookingRequest), bookingRequestId);
        }

        return await LoadTimelineAsync(bookingRequestId);
    }

    private async Task<List<BookingStatusHistoryEntryDto>> LoadTimelineAsync(long bookingRequestId)
    {
        var history = await _unitOfWork.Repository<BookingStatusHistory, long>()
            .GetAllWithSpecAsync(new BookingStatusHistoryByBookingRequestSpecification(bookingRequestId));

        return history.Select(h => new BookingStatusHistoryEntryDto
        {
            PreviousStatus = h.PreviousStatus,
            NewStatus = h.NewStatus,
            ChangedByUserId = h.ChangedByUserId,
            ChangedByName = h.ChangedByUser?.FullName,
            Notes = h.Notes,
            ChangedAt = h.ChangedAt
        }).ToList();
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

        if (booking.Status is not (BookingStatus.Accepted or BookingStatus.AwaitingConfirmation or BookingStatus.Completed))
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

        // Date-plan/slot mismatches are surfaced client-side as a non-blocking warning, not enforced
        // here — the client may deliberately book a vendor for a different day than the plan's
        // nominal date (e.g. a rehearsal, a multi-day event), so the final call stays theirs.
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

        // Relies on the linked VendorAvailability being loaded — either via an explicit Include on the
        // spec that fetched `booking` (BookingRequestByIdSpecification / By-Client / By-Vendor), or
        // EF's change-tracker fixup after this same DbContext instance already loaded/touched that
        // slot earlier in the same request (Accept/Reject/Cancel all load the hold before mapping).
        var slot = booking.VendorAvailability?.FirstOrDefault();
        if (slot is not null)
        {
            dto.SlotStartAt = slot.StartAt;
            dto.SlotEndAt = slot.EndAt;
        }

        return dto;
    }

    /// <summary>
    /// For mutation methods that don't otherwise load the linked slot into the change tracker before
    /// mapping (ConfirmServiceDeliveredAsync, RequestCancellationAsync) — an explicit fetch instead of
    /// relying on fixup, so SlotStartAt/SlotEndAt are never silently missing on the returned DTO.
    /// </summary>
    private async Task<BookingRequestDto> MapBookingWithSlotAsync(BookingRequest booking)
    {
        var dto = MapBooking(booking);
        if (dto.SlotStartAt is null)
        {
            var slots = await _unitOfWork.Repository<VendorAvailability, long>()
                .GetAllWithSpecAsync(new VendorAvailabilityByBookingRequestSpecification(booking.Id));
            var slot = slots.FirstOrDefault();
            if (slot is not null)
            {
                dto.SlotStartAt = slot.StartAt;
                dto.SlotEndAt = slot.EndAt;
            }
        }

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
