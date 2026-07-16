using System.Linq.Expressions;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class PaymentsByVendorSpecification : BaseSpecification<Payment>
{
    public PaymentsByVendorSpecification(long vendorId, PaymentStatus? status = null)
        : base(BuildCriteria(vendorId, status))
    {
    }

    private static Expression<Func<Payment, bool>> BuildCriteria(long vendorId, PaymentStatus? status)
    {
        return status is null
            ? payment => payment.VendorId == vendorId
            : payment => payment.VendorId == vendorId && payment.Status == status.Value;
    }
}
