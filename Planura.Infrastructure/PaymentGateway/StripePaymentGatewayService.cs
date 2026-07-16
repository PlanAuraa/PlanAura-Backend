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

        public async Task<PaymentIntentResult> AuthorizePaymentIntentAsync(AuthorizePaymentIntentRequest request)
        {
            var createOptions = new PaymentIntentCreateOptions
            {
                Amount = request.AmountInSmallestUnit,
                Currency = request.Currency,
                PaymentMethod = request.PaymentMethodId,
                CaptureMethod = "manual",
                Confirm = true,
                Metadata = new Dictionary<string, string>(request.Metadata)
            };

            var requestOptions = new RequestOptions { IdempotencyKey = request.IdempotencyKey };

            var service = new PaymentIntentService();
            PaymentIntent intent;
            try
            {
                intent = await service.CreateAsync(createOptions, requestOptions);
            }
            catch (StripeException ex)
            {
                throw new PaymentDeclinedExeption(ex.StripeError?.Message ?? ex.Message);
            }

            if (intent.Status != "requires_capture")
            {
                var reason = intent.Status == "requires_action"
                    ? "This payment method requires additional verification that isn't supported yet. Please use a different card."
                    : intent.LastPaymentError?.Message ?? "Your card was declined.";
                throw new PaymentDeclinedExeption(reason);
            }

            return new PaymentIntentResult
            {
                PaymentIntentId = intent.Id,
                ClientSecret = intent.ClientSecret,
                Status = intent.Status
            };
        }

        public async Task<PaymentIntentResult> CapturePaymentIntentAsync(CapturePaymentIntentRequest request)
        {
            var requestOptions = new RequestOptions { IdempotencyKey = request.IdempotencyKey };

            var service = new PaymentIntentService();
            var intent = await service.CaptureAsync(request.PaymentIntentId, new PaymentIntentCaptureOptions(), requestOptions);

            return new PaymentIntentResult
            {
                PaymentIntentId = intent.Id,
                ClientSecret = intent.ClientSecret,
                Status = intent.Status
            };
        }

        public async Task CancelPaymentIntentAsync(CancelPaymentIntentRequest request)
        {
            var cancelOptions = new PaymentIntentCancelOptions
            {
                CancellationReason = request.CancellationReason
            };
            var requestOptions = new RequestOptions { IdempotencyKey = request.IdempotencyKey };

            var service = new PaymentIntentService();
            await service.CancelAsync(request.PaymentIntentId, cancelOptions, requestOptions);
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

            var paymentIntent = stripeEvent.Data.Object as PaymentIntent;

            var paymentIntentId = stripeEvent.Type switch
            {
                "payment_intent.succeeded"
                    or "payment_intent.payment_failed"
                    or "payment_intent.canceled"
                    or "payment_intent.amount_capturable_updated"
                    => paymentIntent?.Id,
                "charge.refunded"
                    => (stripeEvent.Data.Object as Charge)?.PaymentIntentId,
                _ => null
            };

            return new PaymentGatewayEvent
            {
                Type = stripeEvent.Type,
                PaymentIntentId = paymentIntentId,
                Metadata = paymentIntent?.Metadata is null ? null : new Dictionary<string, string>(paymentIntent.Metadata)
            };
        }
    }
}
