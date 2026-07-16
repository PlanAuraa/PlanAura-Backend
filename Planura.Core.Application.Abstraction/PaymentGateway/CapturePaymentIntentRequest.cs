namespace Planura.Core.Application.Abstraction.PaymentGateway
{
    public class CapturePaymentIntentRequest
    {
        public string PaymentIntentId { get; set; } = null!;
        public string IdempotencyKey { get; set; } = null!;
    }
}
