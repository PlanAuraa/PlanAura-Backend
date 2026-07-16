namespace Planura.Core.Application.Abstraction.PaymentGateway
{
    public class PaymentGatewayEvent
    {
        public string Type { get; set; } = null!;
        public string? PaymentIntentId { get; set; }
        public IReadOnlyDictionary<string, string>? Metadata { get; set; }
    }
}
