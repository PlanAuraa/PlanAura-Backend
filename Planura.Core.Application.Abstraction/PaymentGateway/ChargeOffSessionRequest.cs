namespace Planura.Core.Application.Abstraction.PaymentGateway
{
    /// <summary>
    /// Charges a saved card off-session (client not present) — used by the remainder-charge job (Phase 2)
    /// to collect the outstanding balance on a deposit booking. Requires the Stripe Customer and the saved
    /// PaymentMethod captured at booking time. The IdempotencyKey MUST be stable per remainder charge
    /// (remainder-{paymentId}) so repeated/overlapping calls collapse to a single charge at Stripe.
    /// </summary>
    public class ChargeOffSessionRequest
    {
        public long AmountInSmallestUnit { get; set; }
        public string Currency { get; set; } = null!;
        public string CustomerId { get; set; } = null!;
        public string PaymentMethodId { get; set; } = null!;
        public string IdempotencyKey { get; set; } = null!;
        public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }
}
