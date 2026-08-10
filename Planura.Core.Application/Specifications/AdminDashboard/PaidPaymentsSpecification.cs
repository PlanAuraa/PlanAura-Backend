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
    // Fully-captured revenue: the full-payment path (Completed) and the deposit path once its remainder
    // was collected (FullyPaid). Refunded drops out, so refunds are excluded from revenue.
    public PaidPaymentsSpecification()
        : base(p => p.Status == PaymentStatus.Completed || p.Status == PaymentStatus.FullyPaid)
    {
    }
}