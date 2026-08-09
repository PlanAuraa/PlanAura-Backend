using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

/// <summary>
/// Finds the Payment whose remainder charge PaymentIntent matches this gateway reference. The remainder
/// PI has a different id than the deposit PI (GatewayReference), so the webhook reconciler tries this after
/// the deposit lookup to finalize an on-session (SCA) remainder payment completed in the browser.
/// </summary>
public class PaymentByRemainderGatewayReferenceSpecification : BaseSpecification<Payment>
{
    public PaymentByRemainderGatewayReferenceSpecification(string remainderGatewayReference)
        : base(payment => payment.RemainderGatewayReference == remainderGatewayReference)
    {
    }
}
