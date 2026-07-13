namespace Planura.Core.Application.Abstraction.PaymentGateway
{
    public class PaymentIntentResult
    {
        public string PaymentIntentId { get; set; } = null!;
        public string ClientSecret { get; set; } = null!;
    }
}
