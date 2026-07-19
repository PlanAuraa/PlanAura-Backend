namespace Planura.Core.Application.Abstraction.PaymentGateway
{
    public class RefundPaymentIntentRequest
    {
        public string PaymentIntentId { get; set; } = null!;
        public string IdempotencyKey { get; set; } = null!;

        /// <summary>Full refund when null; otherwise a partial refund in the gateway's smallest currency unit.</summary>
        public long? AmountInSmallestUnit { get; set; }

        /// <summary>Stripe refund reason: duplicate | fraudulent | requested_by_customer.</summary>
        public string? Reason { get; set; }
    }
}
