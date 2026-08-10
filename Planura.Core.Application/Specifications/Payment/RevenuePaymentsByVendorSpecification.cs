using System.Linq.Expressions;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

/// <summary>
/// A vendor's revenue-counted payments: fully-captured charges only. That is both the full-payment path
/// (<see cref="PaymentStatus.Completed"/>) and the deposit path once its remainder was collected
/// (<see cref="PaymentStatus.FullyPaid"/>). Deposit-only states (DepositPaid_RemainderDue / RemainderFailed /
/// RemainderCharging) are NOT revenue — the remainder isn't captured yet — and Refunded drops out, so a
/// refunded payment stops counting toward the vendor's revenue.
/// </summary>
public class RevenuePaymentsByVendorSpecification : BaseSpecification<Payment>
{
    public RevenuePaymentsByVendorSpecification(long vendorId)
        : base(payment => payment.VendorId == vendorId
            && (payment.Status == PaymentStatus.Completed || payment.Status == PaymentStatus.FullyPaid))
    {
    }
}
