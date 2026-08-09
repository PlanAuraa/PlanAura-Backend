namespace Planura.Core.Application.Abstraction.PaymentGateway
{
    /// <summary>
    /// Charges a saved card ON-SESSION (client present) — used by the client "pay remainder now" flow
    /// (Phase 3). Unlike the off-session job charge, this supports SCA: if authentication is required the
    /// PaymentIntent comes back as requires_action with a client secret the frontend completes, rather than
    /// throwing. Same saved Customer + PaymentMethod captured at booking time.
    /// </summary>
    public class ChargeOnSessionRequest
    {
        public long AmountInSmallestUnit { get; set; }
        public string Currency { get; set; } = null!;
        public string CustomerId { get; set; } = null!;
        public string PaymentMethodId { get; set; } = null!;
        public string IdempotencyKey { get; set; } = null!;
        public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }
}
