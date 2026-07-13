using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class PaymentByGatewayReferenceSpecification : BaseSpecification<Payment>
{
    public PaymentByGatewayReferenceSpecification(string gatewayReference)
        : base(payment => payment.GatewayReference == gatewayReference)
    {
    }
}
