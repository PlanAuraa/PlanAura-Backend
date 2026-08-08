namespace Planura.Core.Application.Abstraction.PaymentGateway
{
    public class AuthorizePaymentIntentRequest
    {
        public long AmountInSmallestUnit { get; set; }
        public string Currency { get; set; } = null!;
        public string PaymentMethodId { get; set; } = null!;
        public string IdempotencyKey { get; set; } = null!;
        public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

        // Deposit / partial-payment (Phase 2). When set, the PaymentIntent is attached to this Stripe
        // Customer and (with SaveCardForOffSession) saves the card for later off-session remainder charges.
        // Both are null/false on the full-payment path, which never needs to charge again.
        public string? CustomerId { get; set; }
        public bool SaveCardForOffSession { get; set; }
    }
}
