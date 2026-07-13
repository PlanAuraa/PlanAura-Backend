using AutoMapper;
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

    public PaymentService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        INotificationService notificationService,
        IPaymentGatewayService paymentGatewayService,
        IOptions<StripeOptions> stripeOptions)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _notificationService = notificationService;
        _paymentGatewayService = paymentGatewayService;
        _stripeOptions = stripeOptions.Value;
    }

    public async Task<PaymentOptionsDto> GetPaymentOptionsAsync(long bookingRequestId, long clientUserId)
    {
        var clientId = await ResolveClientIdAsync(clientUserId);

        var booking = await _unitOfWork.Repository<BookingRequest, long>().GetAsync(bookingRequestId);
        if (booking is null || booking.ClientId != clientId)
        {
            throw new NotFoundExeption(nameof(BookingRequest), bookingRequestId);
        }

        var isPayable = booking.Status == BookingStatus.Accepted
            && booking.PaymentStatus == BookingPaymentStatus.Unpaid
            && booking.AgreedPrice is > 0;

        return new PaymentOptionsDto
        {
            BookingRequestId = booking.Id,
            AmountDue = booking.AgreedPrice ?? 0m,
            Currency = _stripeOptions.DefaultCurrency,
            IsPayable = isPayable,
            PublishableKey = _stripeOptions.PublishableKey
        };
    }

    public async Task<InitiatePaymentResultDto> InitiatePaymentAsync(long bookingRequestId, long clientUserId, InitiatePaymentDto dto)
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
                $"Cannot initiate payment for a booking request with status '{booking.Status}'. The vendor must accept it first.");
        }

        if (booking.PaymentStatus != BookingPaymentStatus.Unpaid)
        {
            throw new BadRequestExeption(
                $"This booking's payment status is '{booking.PaymentStatus}' and cannot be paid again.");
        }

        if (booking.AgreedPrice is null || booking.AgreedPrice <= 0)
        {
            throw new BadRequestExeption("This booking does not have an agreed price to charge.");
        }

        var paymentRepo = _unitOfWork.Repository<Payment, long>();
        var existingPending = await paymentRepo.GetWithSpecAsync(
            new PendingPaymentByBookingRequestSpecification(bookingRequestId));
        if (existingPending is not null)
        {
            throw new BadRequestExeption("A payment is already in progress for this booking request.");
        }

        var amount = booking.AgreedPrice.Value;

        var payment = new Payment
        {
            BookingRequestId = bookingRequestId,
            ClientId = clientId,
            VendorId = booking.VendorId,
            Amount = amount,
            Status = PaymentStatus.Pending
        };

        await paymentRepo.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        try
        {
            var intentResult = await _paymentGatewayService.CreatePaymentIntentAsync(new CreatePaymentIntentRequest
            {
                AmountInSmallestUnit = ConvertToSmallestUnit(amount),
                Currency = _stripeOptions.DefaultCurrency.ToLowerInvariant(),
                IdempotencyKey = $"booking-{bookingRequestId}-payment-{payment.Id}",
                Metadata = new Dictionary<string, string>
                {
                    ["booking_request_id"] = bookingRequestId.ToString(),
                    ["payment_id"] = payment.Id.ToString()
                }
            });

            payment.GatewayReference = intentResult.PaymentIntentId;
            paymentRepo.Update(payment);
            await _unitOfWork.SaveChangesAsync();

            return new InitiatePaymentResultDto
            {
                PaymentId = payment.Id,
                ClientSecret = intentResult.ClientSecret,
                PublishableKey = _stripeOptions.PublishableKey,
                Amount = amount,
                Currency = _stripeOptions.DefaultCurrency
            };
        }
        catch
        {
            payment.Status = PaymentStatus.Failed;
            paymentRepo.Update(payment);
            await _unitOfWork.SaveChangesAsync();
            throw;
        }
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
        if (payment is null)
        {
            return;
        }

        switch (gatewayEvent.Type)
        {
            case "payment_intent.succeeded":
                await HandlePaymentSucceededAsync(payment);
                break;
            case "payment_intent.payment_failed":
                await HandlePaymentFailedAsync(payment);
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
        payment.Status = PaymentStatus.Completed;
        payment.PaidAt = DateTimeOffset.UtcNow;
        _unitOfWork.Repository<Payment, long>().Update(payment);

        var bookingRepo = _unitOfWork.Repository<BookingRequest, long>();
        var booking = await bookingRepo.GetAsync(payment.BookingRequestId);
        if (booking is not null)
        {
            booking.PaymentStatus = BookingPaymentStatus.Paid;
            booking.UpdatedAt = DateTimeOffset.UtcNow;
            bookingRepo.Update(booking);
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

    private static long ConvertToSmallestUnit(decimal amount)
        => Convert.ToInt64(Math.Round(amount * 100m, MidpointRounding.AwayFromZero));
}
