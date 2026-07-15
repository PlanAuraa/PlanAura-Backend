namespace Planura.Core.Application.Abstraction.PaymentGateway
{
    public interface IPaymentGatewayService
    {
        Task<PaymentIntentResult> CreatePaymentIntentAsync(CreatePaymentIntentRequest request);

        PaymentGatewayEvent ConstructWebhookEvent(string rawJson, string signatureHeader);
    }
}
