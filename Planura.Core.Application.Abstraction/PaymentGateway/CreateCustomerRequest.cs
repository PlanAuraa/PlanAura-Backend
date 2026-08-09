namespace Planura.Core.Application.Abstraction.PaymentGateway
{
    /// <summary>
    /// Creates a Stripe Customer for a client so their card can be saved and charged off-session later
    /// (deposit path remainder charge, Phase 2). Created lazily on the client's first deposit booking and
    /// then reused via the stored StripeCustomerId.
    /// </summary>
    public class CreateCustomerRequest
    {
        public string? Email { get; set; }
        public string? Name { get; set; }
        public string IdempotencyKey { get; set; } = null!;
        public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }
}
