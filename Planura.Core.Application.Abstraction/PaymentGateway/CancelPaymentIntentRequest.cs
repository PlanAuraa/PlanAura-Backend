namespace Planura.Core.Application.Abstraction.PaymentGateway
{
    public class CancelPaymentIntentRequest
    {
        public string PaymentIntentId { get; set; } = null!;
        public string IdempotencyKey { get; set; } = null!;

        /// <summary>Stripe cancellation_reason: duplicate | fraudulent | requested_by_customer | abandoned.</summary>
        public string? CancellationReason { get; set; }
    }
}
