using System.Linq.Expressions;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class PaymentsByClientSpecification : BaseSpecification<Payment>
{
    public PaymentsByClientSpecification(long clientId, PaymentStatus? status, int? skip, int? take)
        : base(BuildCriteria(clientId, status))
    {
        ApplyOrderByDescending(payment => payment.CreatedAt);

        if (skip is not null && take is not null)
        {
            ApplyPaging(skip.Value, take.Value);
        }
    }

    private static Expression<Func<Payment, bool>> BuildCriteria(long clientId, PaymentStatus? status)
    {
        return status is null
            ? payment => payment.ClientId == clientId
            : payment => payment.ClientId == clientId && payment.Status == status.Value;
    }
}
