using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Planura.Core.Application.Specifications.AdminDashboard;

public class PaidPaymentsSpecification : BaseSpecification<Payment>
{
    // Every payment with money currently collected: the full-payment path (Completed), the deposit path at
    // any point after the deposit is captured (DepositPaid_RemainderDue / RemainderCharging /
    // RemainderFailed / FullyPaid), and refunded rows (Refunded / PartiallyRefunded still hold some or all
    // of the captured amount as far as Payment.AmountCapturedExpression is concerned - the actual refund is
    // netted separately via RefundedAmount, not by dropping the row here). Callers must sum via
    // Payment.AmountCapturedExpression, not TotalAmount/Amount, since a deposit-only row's revenue is its
    // deposit, not its total.
    public PaidPaymentsSpecification()
        : base(p => p.Status == PaymentStatus.Completed
            || p.Status == PaymentStatus.DepositPaid_RemainderDue
            || p.Status == PaymentStatus.RemainderCharging
            || p.Status == PaymentStatus.RemainderFailed
            || p.Status == PaymentStatus.FullyPaid
            || p.Status == PaymentStatus.Refunded
            || p.Status == PaymentStatus.PartiallyRefunded)
    {
    }
}