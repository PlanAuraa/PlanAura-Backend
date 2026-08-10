using System.Linq.Expressions;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

/// <summary>
/// A vendor's payments with money currently collected: the full-payment path
/// (<see cref="PaymentStatus.Completed"/>), the deposit path at any point once the deposit is captured
/// (<see cref="PaymentStatus.DepositPaid_RemainderDue"/>/<see cref="PaymentStatus.RemainderCharging"/>/
/// <see cref="PaymentStatus.RemainderFailed"/>/<see cref="PaymentStatus.FullyPaid"/>), and refunded rows
/// (<see cref="PaymentStatus.Refunded"/>/<see cref="PaymentStatus.PartiallyRefunded"/> - the refund itself is
/// netted separately, not by excluding the row here). Callers must sum via
/// <see cref="Payment.AmountCapturedExpression"/>, not TotalAmount/Amount, since a deposit-only row's revenue
/// is its deposit, not the booking's full total.
/// </summary>
public class RevenuePaymentsByVendorSpecification : BaseSpecification<Payment>
{
    public RevenuePaymentsByVendorSpecification(long vendorId)
        : base(payment => payment.VendorId == vendorId
            && (payment.Status == PaymentStatus.Completed
                || payment.Status == PaymentStatus.DepositPaid_RemainderDue
                || payment.Status == PaymentStatus.RemainderCharging
                || payment.Status == PaymentStatus.RemainderFailed
                || payment.Status == PaymentStatus.FullyPaid
                || payment.Status == PaymentStatus.Refunded
                || payment.Status == PaymentStatus.PartiallyRefunded))
    {
    }
}
