using Microsoft.Extensions.Options;
using Planura.Core.Application.Abstraction.PaymentGateway;
using Planura.Core.Application.Common;
using Planura.Shared.Errors.Models;
using Stripe;

namespace Planura.Infrastructure.PaymentGateway
{
    public class StripePaymentGatewayService : IPaymentGatewayService
    {
        private readonly StripeOptions _options;

        public StripePaymentGatewayService(IOptions<StripeOptions> options)
        {
            _options = options.Value;
            StripeConfiguration.ApiKey = _options.SecretKey;
        }

        public async Task<PaymentIntentResult> CreatePaymentIntentAsync(CreatePaymentIntentRequest request)
        {
            var createOptions = new PaymentIntentCreateOptions
            {
                Amount = request.AmountInSmallestUnit,
                Currency = request.Currency,
                Metadata = new Dictionary<string, string>(request.Metadata)
            };

            var requestOptions = new RequestOptions { IdempotencyKey = request.IdempotencyKey };

            var service = new PaymentIntentService();
            var intent = await service.CreateAsync(createOptions, requestOptions);

            return new PaymentIntentResult
            {
                PaymentIntentId = intent.Id,
                ClientSecret = intent.ClientSecret
            };
        }

        public PaymentGatewayEvent ConstructWebhookEvent(string rawJson, string signatureHeader)
        {
            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(rawJson, signatureHeader, _options.WebhookSecret);
            }
            catch (StripeException ex)
            {
                throw new BadRequestExeption($"Invalid Stripe webhook signature: {ex.Message}");
            }

            var paymentIntentId = stripeEvent.Type switch
            {
                "payment_intent.succeeded" or "payment_intent.payment_failed"
                    => (stripeEvent.Data.Object as PaymentIntent)?.Id,
                "charge.refunded"
                    => (stripeEvent.Data.Object as Charge)?.PaymentIntentId,
                _ => null
            };

            return new PaymentGatewayEvent
            {
                Type = stripeEvent.Type,
                PaymentIntentId = paymentIntentId
            };
        }
    }
}
