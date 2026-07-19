namespace Planura.Core.Application.Abstraction.PaymentGateway
{
    public interface IPaymentGatewayService
    {
        /// <summary>Creates and immediately confirms a manual-capture PaymentIntent, placing an authorization hold on the card.</summary>
        Task<PaymentIntentResult> AuthorizePaymentIntentAsync(AuthorizePaymentIntentRequest request);

        /// <summary>Captures a previously authorized (manual-capture) PaymentIntent, charging the held funds.</summary>
        Task<PaymentIntentResult> CapturePaymentIntentAsync(CapturePaymentIntentRequest request);

        /// <summary>Voids a previously authorized (not yet captured) PaymentIntent, releasing the held funds.</summary>
        Task CancelPaymentIntentAsync(CancelPaymentIntentRequest request);

        /// <summary>Refunds a previously captured (Completed) PaymentIntent, in full or in part.</summary>
        Task<RefundResult> RefundPaymentIntentAsync(RefundPaymentIntentRequest request);

        PaymentGatewayEvent ConstructWebhookEvent(string rawJson, string signatureHeader);
    }
}
