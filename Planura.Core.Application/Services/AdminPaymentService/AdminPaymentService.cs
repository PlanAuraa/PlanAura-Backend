using Microsoft.EntityFrameworkCore;
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

            // AmountPaid/RemainingAmount are derived via Payment.GetAmountCaptured(), a plain C# method that
            // EF cannot translate to SQL - so the page is materialized first (with the navigations the
            // projection needs explicitly included, since there's no lazy-loading proxy configured) and then
            // projected in-memory.
            var pageEntities = queryable
                .Include(p => p.Client).ThenInclude(c => c.User)
                .Include(p => p.Vendor)
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = pageEntities
                .Select(p =>
                {
                    var totalAmount = p.TotalAmount ?? p.Amount;
                    var amountPaid = p.GetAmountCaptured();
                    return new AdminPaymentListItemDto
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
                        TotalAmount = totalAmount,
                        AmountPaid = amountPaid,
                        RemainingAmount = Math.Max(totalAmount - amountPaid, 0m),
                        RefundedAmount = p.RefundedAmount ?? 0m,
                        Status = p.Status,
                        PaymentMethod = p.PaymentMethod,
                        GatewayReference = p.GatewayReference,
                        PaidAt = p.PaidAt,
                        RefundedAt = p.RefundedAt,
                        CreatedAt = p.CreatedAt
                    };
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
                // Gross Revenue = every dollar ever captured, GROSS of refunds (deposits count the moment
                // they're captured, not only once a booking reaches FullyPaid/Completed - a deposit-only
                // payment contributes its deposit, not its total). Payment.AmountCapturedExpression already
                // returns 0 for every non-captured state, so summing the whole table (no Where) is safe and
                // correct - no separate status filter needed.
                GrossRevenue = queryable.Sum(Payment.AmountCapturedExpression),
                // The real amount returned to clients, not the full captured amount of every Refunded row
                // (which overstated any partial refund, since no refunded amount was ever tracked before).
                RefundedAmount = queryable.Sum(p => (decimal?)p.RefundedAmount) ?? 0m,
                FailedPaymentCount = queryable.Count(p => p.Status == PaymentStatus.Failed),
                // Both full holds (Authorized) and deposit holds (DepositAuthorized) are money held on a
                // card awaiting the vendor — both count as pending authorization.
                PendingAuthorizationCount = queryable.Count(p =>
                    p.Status == PaymentStatus.Authorized || p.Status == PaymentStatus.DepositAuthorized)
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

            // Refundable = a fully-captured payment: Completed (full-payment path) or FullyPaid (deposit path
            // whose remainder was collected). Deposit-only states (DepositPaid_RemainderDue / RemainderFailed /
            // RemainderCharging) are not refundable — a deposit-only cancellation is non-refundable by policy.
            if (payment.Status is not (PaymentStatus.Completed or PaymentStatus.FullyPaid))
            {
                throw new BadRequestExeption(
                    $"Only a fully-captured payment (Completed or FullyPaid) can be refunded (current status: '{payment.Status}').");
            }

            if (string.IsNullOrWhiteSpace(payment.GatewayReference))
            {
                throw new BadRequestExeption("This payment has no gateway reference and cannot be refunded through Stripe.");
            }

            decimal actuallyRefunded;
            if (payment.Status == PaymentStatus.FullyPaid)
            {
                // Deposit path: the total was captured across TWO PaymentIntents (the deposit PI and the
                // remainder PI), so the refund must hit both. Basis is the amount actually captured.
                actuallyRefunded = await RefundFullyPaidDepositAsync(payment, dto.Amount);
            }
            else
            {
                // Full-payment path: a single captured PaymentIntent — unchanged behavior.
                long? amountInSmallestUnit = dto.Amount is null
                    ? null
                    : StripeAmountConverter.ToSmallestUnit(dto.Amount.Value);

                var result = await _paymentGatewayService.RefundPaymentIntentAsync(new RefundPaymentIntentRequest
                {
                    PaymentIntentId = payment.GatewayReference!,
                    IdempotencyKey = $"admin-refund-{payment.Id}-{DateTimeOffset.UtcNow.Ticks}",
                    AmountInSmallestUnit = amountInSmallestUnit,
                    Reason = "requested_by_customer"
                });
                actuallyRefunded = StripeAmountConverter.FromSmallestUnit(result.AmountRefundedInSmallestUnit);
            }

            // A refund for less than the full captured amount leaves the payment PartiallyRefunded rather than
            // Refunded, so admin reporting stops treating every refund as if the whole payment came back. The
            // small epsilon absorbs decimal rounding across the two-PaymentIntent split on the deposit path.
            payment.RefundedAmount = (payment.RefundedAmount ?? 0m) + actuallyRefunded;
            var capturedBasis = payment.TotalAmount ?? payment.Amount;
            payment.Status = payment.RefundedAmount >= capturedBasis - 0.01m
                ? PaymentStatus.Refunded
                : PaymentStatus.PartiallyRefunded;
            payment.RefundedAt = DateTimeOffset.UtcNow;
            payment.RefundReason = $"Admin refund (admin #{adminId}): {dto.Reason}";
            repo.Update(payment);

            var bookingRepo = _unitOfWork.Repository<BookingRequest, long>();
            var booking = await bookingRepo.GetAsync(payment.BookingRequestId);
            if (booking is not null)
            {
                // BookingPaymentStatus stays a coarse cache by design - it has no partial-refund value of its
                // own, so both Refunded and PartiallyRefunded map to the same "a refund happened" state here.
                // The fine-grained distinction lives on Payment.Status, which is what the UI actually reads.
                booking.PaymentStatus = BookingPaymentStatus.Refunded;
                booking.UpdatedAt = DateTimeOffset.UtcNow;
                bookingRepo.Update(booking);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Admin {AdminId} refunded {Amount} on Payment {PaymentId} (PaymentIntent {PaymentIntentId}), new status {Status}. Reason: {Reason}",
                adminId, actuallyRefunded, payment.Id, payment.GatewayReference, payment.Status, dto.Reason);

            var totalAmount = payment.TotalAmount ?? payment.Amount;
            var amountPaid = payment.GetAmountCaptured();
            return new AdminPaymentListItemDto
            {
                PaymentId = payment.Id,
                BookingRequestId = payment.BookingRequestId,
                ClientId = payment.ClientId,
                VendorId = payment.VendorId,
                Amount = payment.Amount,
                TotalAmount = totalAmount,
                AmountPaid = amountPaid,
                RemainingAmount = Math.Max(totalAmount - amountPaid, 0m),
                RefundedAmount = payment.RefundedAmount ?? 0m,
                Status = payment.Status,
                PaymentMethod = payment.PaymentMethod,
                GatewayReference = payment.GatewayReference,
                PaidAt = payment.PaidAt,
                RefundedAt = payment.RefundedAt,
                CreatedAt = payment.CreatedAt
            };
        }

        /// <summary>
        /// Refunds a fully-paid deposit booking across its two PaymentIntents. A full refund (null amount)
        /// refunds each PI in full; a partial amount is allocated deposit-PI-first, then the remainder PI, so
        /// the total refunded matches the requested amount (capped at the amount actually captured). Returns
        /// the sum actually refunded across both legs, as reported by the gateway.
        /// </summary>
        private async Task<decimal> RefundFullyPaidDepositAsync(Payment payment, decimal? requestedAmount)
        {
            var depositCaptured = payment.DepositAmount ?? payment.Amount;
            var remainderCaptured = (payment.TotalAmount ?? depositCaptured) - depositCaptured;

            decimal depositPortion;
            decimal remainderPortion;
            if (requestedAmount is null)
            {
                depositPortion = depositCaptured;
                remainderPortion = remainderCaptured;
            }
            else
            {
                var amount = Math.Min(requestedAmount.Value, depositCaptured + remainderCaptured);
                depositPortion = Math.Min(amount, depositCaptured);
                remainderPortion = amount - depositPortion;
            }

            var totalRefunded = 0m;

            if (depositPortion > 0m)
            {
                var result = await _paymentGatewayService.RefundPaymentIntentAsync(new RefundPaymentIntentRequest
                {
                    PaymentIntentId = payment.GatewayReference!,
                    IdempotencyKey = $"admin-refund-{payment.Id}-deposit-{DateTimeOffset.UtcNow.Ticks}",
                    // null amount => Stripe refunds this PI in full.
                    AmountInSmallestUnit = requestedAmount is null ? null : StripeAmountConverter.ToSmallestUnit(depositPortion),
                    Reason = "requested_by_customer"
                });
                totalRefunded += StripeAmountConverter.FromSmallestUnit(result.AmountRefundedInSmallestUnit);
            }

            if (remainderPortion > 0m && !string.IsNullOrWhiteSpace(payment.RemainderGatewayReference))
            {
                var result = await _paymentGatewayService.RefundPaymentIntentAsync(new RefundPaymentIntentRequest
                {
                    PaymentIntentId = payment.RemainderGatewayReference!,
                    IdempotencyKey = $"admin-refund-{payment.Id}-remainder-{DateTimeOffset.UtcNow.Ticks}",
                    AmountInSmallestUnit = requestedAmount is null ? null : StripeAmountConverter.ToSmallestUnit(remainderPortion),
                    Reason = "requested_by_customer"
                });
                totalRefunded += StripeAmountConverter.FromSmallestUnit(result.AmountRefundedInSmallestUnit);
            }

            return totalRefunded;
        }
    }
}
