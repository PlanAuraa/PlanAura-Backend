using Microsoft.Extensions.Logging;
using Planura.Core.Application.Abstraction.PaymentGateway;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models;
using Planura.Core.Application.Models.AdminPayment;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services.AdminPayment
{
    /// <summary>
    /// Backs the "Payments &amp; Transactions" admin page (AdminDashboardPlan.md 2.9). The refund path
    /// depends on IPaymentGatewayService.RefundPaymentIntentAsync, which did not exist before this
    /// feature was built - see the abstraction addition in Planura.Core.Application.Abstraction.
    /// </summary>
    public class AdminPaymentService : IAdminPaymentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPaymentGatewayService _paymentGatewayService;
        private readonly ILogger<AdminPaymentService> _logger;

        public AdminPaymentService(
            IUnitOfWork unitOfWork,
            IPaymentGatewayService paymentGatewayService,
            ILogger<AdminPaymentService> logger)
        {
            _unitOfWork = unitOfWork;
            _paymentGatewayService = paymentGatewayService;
            _logger = logger;
        }

        public Task<PagedResult<AdminPaymentListItemDto>> ListPaymentsAsync(AdminPaymentFilterDto filter)
        {
            var page = filter.Page < 1 ? 1 : filter.Page;
            var pageSize = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;

            var queryable = _unitOfWork.Repository<Payment, long>().GetQueryable();

            if (filter.Status is not null)
            {
                queryable = queryable.Where(p => p.Status == filter.Status);
            }

            if (filter.VendorId is not null)
            {
                queryable = queryable.Where(p => p.VendorId == filter.VendorId);
            }

            if (filter.ClientId is not null)
            {
                queryable = queryable.Where(p => p.ClientId == filter.ClientId);
            }

            if (filter.From is not null)
            {
                queryable = queryable.Where(p => p.CreatedAt >= filter.From);
            }

            if (filter.To is not null)
            {
                queryable = queryable.Where(p => p.CreatedAt <= filter.To);
            }

            var totalCount = queryable.Count();

            var items = queryable
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new AdminPaymentListItemDto
                {
                    PaymentId = p.Id,
                    BookingRequestId = p.BookingRequestId,
                    ClientId = p.ClientId,
                    ClientName = p.Client.User.FullName,
                    ClientEmail = p.Client.User.Email,
                    ClientPhone = p.Client.User.PhoneNumber,
                    VendorId = p.VendorId,
                    VendorName = p.Vendor.BusinessName,
                    Amount = p.Amount,
                    Status = p.Status,
                    PaymentMethod = p.PaymentMethod,
                    GatewayReference = p.GatewayReference,
                    PaidAt = p.PaidAt,
                    RefundedAt = p.RefundedAt,
                    CreatedAt = p.CreatedAt
                })
                .ToList();

            return Task.FromResult(new PagedResult<AdminPaymentListItemDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }

        public Task<AdminPaymentSummaryDto> GetSummaryAsync()
        {
            var queryable = _unitOfWork.Repository<Payment, long>().GetQueryable();

            var summary = new AdminPaymentSummaryDto
            {
                GrossRevenue = queryable
                    .Where(p => p.Status == PaymentStatus.Completed)
                    .Sum(p => (decimal?)p.Amount) ?? 0m,
                RefundedAmount = queryable
                    .Where(p => p.Status == PaymentStatus.Refunded)
                    .Sum(p => (decimal?)p.Amount) ?? 0m,
                FailedPaymentCount = queryable.Count(p => p.Status == PaymentStatus.Failed),
                PendingAuthorizationCount = queryable.Count(p => p.Status == PaymentStatus.Authorized)
            };

            return Task.FromResult(summary);
        }

        public async Task<AdminPaymentListItemDto> RefundPaymentAsync(long paymentId, long adminId, RefundPaymentDto dto)
        {
            var repo = _unitOfWork.Repository<Payment, long>();
            var payment = await repo.GetAsync(paymentId);

            if (payment is null)
            {
                throw new NotFoundExeption(nameof(Payment), paymentId);
            }

            if (payment.Status != PaymentStatus.Completed)
            {
                throw new BadRequestExeption(
                    $"Only a Completed payment can be refunded (current status: '{payment.Status}').");
            }

            if (string.IsNullOrWhiteSpace(payment.GatewayReference))
            {
                throw new BadRequestExeption("This payment has no gateway reference and cannot be refunded through Stripe.");
            }

            long? amountInSmallestUnit = dto.Amount is null
                ? null
                : StripeAmountConverter.ToSmallestUnit(dto.Amount.Value);

            await _paymentGatewayService.RefundPaymentIntentAsync(new RefundPaymentIntentRequest
            {
                PaymentIntentId = payment.GatewayReference!,
                IdempotencyKey = $"admin-refund-{payment.Id}-{DateTimeOffset.UtcNow.Ticks}",
                AmountInSmallestUnit = amountInSmallestUnit,
                Reason = "requested_by_customer"
            });

            // The domain's PaymentStatus enum has no "PartiallyRefunded" state, so - like the existing
            // charge.refunded webhook handler - a refund of any size marks the payment Refunded locally.
            // Stripe itself tracks the exact (possibly partial) amount refunded.
            payment.Status = PaymentStatus.Refunded;
            payment.RefundedAt = DateTimeOffset.UtcNow;
            payment.RefundReason = $"Admin refund (admin #{adminId}): {dto.Reason}";
            repo.Update(payment);

            var bookingRepo = _unitOfWork.Repository<BookingRequest, long>();
            var booking = await bookingRepo.GetAsync(payment.BookingRequestId);
            if (booking is not null)
            {
                booking.PaymentStatus = BookingPaymentStatus.Refunded;
                booking.UpdatedAt = DateTimeOffset.UtcNow;
                bookingRepo.Update(booking);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Admin {AdminId} refunded Payment {PaymentId} (PaymentIntent {PaymentIntentId}). Reason: {Reason}",
                adminId, payment.Id, payment.GatewayReference, dto.Reason);

            return new AdminPaymentListItemDto
            {
                PaymentId = payment.Id,
                BookingRequestId = payment.BookingRequestId,
                ClientId = payment.ClientId,
                VendorId = payment.VendorId,
                Amount = payment.Amount,
                Status = payment.Status,
                PaymentMethod = payment.PaymentMethod,
                GatewayReference = payment.GatewayReference,
                PaidAt = payment.PaidAt,
                RefundedAt = payment.RefundedAt,
                CreatedAt = payment.CreatedAt
            };
        }
    }
}
