namespace Planura.Core.Application.Abstraction.PaymentGateway
{
    public class CreatePaymentIntentRequest
    {
        public long AmountInSmallestUnit { get; set; }
        public string Currency { get; set; } = null!;
        public string IdempotencyKey { get; set; } = null!;
        public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }
}
