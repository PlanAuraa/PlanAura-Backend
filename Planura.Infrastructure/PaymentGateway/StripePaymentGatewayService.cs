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

        public async Task<string> CreateCustomerAsync(CreateCustomerRequest request)
        {
            var createOptions = new CustomerCreateOptions
            {
                Email = request.Email,
                Name = request.Name,
                Metadata = new Dictionary<string, string>(request.Metadata)
            };

            var requestOptions = new RequestOptions { IdempotencyKey = request.IdempotencyKey };

            var service = new CustomerService();
            Customer customer;
            try
            {
                customer = await service.CreateAsync(createOptions, requestOptions);
            }
            catch (StripeException ex)
            {
                throw new PaymentDeclinedExeption(ex.StripeError?.Message ?? ex.Message);
            }

            return customer.Id;
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
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                    AllowRedirects = "never",
                },
                Metadata = new Dictionary<string, string>(request.Metadata)
            };

            // Deposit path: attach to the client's Customer and save the card for off-session future use so
            // the remainder can be charged later without the client present. Stripe attaches the PaymentMethod
            // to the Customer when this PaymentIntent is confirmed.
            if (!string.IsNullOrWhiteSpace(request.CustomerId))
            {
                createOptions.Customer = request.CustomerId;
            }
            if (request.SaveCardForOffSession)
            {
                createOptions.SetupFutureUsage = "off_session";
            }

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

        public async Task<PaymentIntentResult> ChargeOffSessionAsync(ChargeOffSessionRequest request)
        {
            var createOptions = new PaymentIntentCreateOptions
            {
                Amount = request.AmountInSmallestUnit,
                Currency = request.Currency,
                Customer = request.CustomerId,
                PaymentMethod = request.PaymentMethodId,
                Confirm = true,
                OffSession = true,
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
                // Any off-session failure (insufficient funds, card declined, authentication_required/SCA)
                // surfaces here and is treated identically by the caller — no SCA-specific branch (Phase 2).
                throw new PaymentDeclinedExeption(ex.StripeError?.Message ?? ex.Message);
            }

            if (intent.Status != "succeeded")
            {
                throw new PaymentDeclinedExeption(intent.LastPaymentError?.Message ?? "The off-session charge did not succeed.");
            }

            return new PaymentIntentResult
            {
                PaymentIntentId = intent.Id,
                ClientSecret = intent.ClientSecret,
                Status = intent.Status
            };
        }

        public async Task<PaymentIntentResult> ChargeOnSessionAsync(ChargeOnSessionRequest request)
        {
            var createOptions = new PaymentIntentCreateOptions
            {
                Amount = request.AmountInSmallestUnit,
                Currency = request.Currency,
                Customer = request.CustomerId,
                PaymentMethod = request.PaymentMethodId,
                Confirm = true,
                // On-session (client present): do NOT set OffSession, so SCA is allowed. AllowRedirects
                // "never" keeps 3DS to the client-secret flow (frontend confirmCardPayment) — no redirect.
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                    AllowRedirects = "never",
                },
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
                // Hard card errors (declined, insufficient funds) throw; SCA does NOT — it returns
                // requires_action below for the frontend to complete.
                throw new PaymentDeclinedExeption(ex.StripeError?.Message ?? ex.Message);
            }

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

        public async Task<RefundResult> RefundPaymentIntentAsync(RefundPaymentIntentRequest request)
        {
            var createOptions = new RefundCreateOptions
            {
                PaymentIntent = request.PaymentIntentId,
                Amount = request.AmountInSmallestUnit,
                Reason = request.Reason
            };

            var requestOptions = new RequestOptions { IdempotencyKey = request.IdempotencyKey };

            var service = new RefundService();
            Refund refund;
            try
            {
                refund = await service.CreateAsync(createOptions, requestOptions);
            }
            catch (StripeException ex)
            {
                throw new PaymentDeclinedExeption(ex.StripeError?.Message ?? ex.Message);
            }

            return new RefundResult
            {
                RefundId = refund.Id,
                Status = refund.Status,
                AmountRefundedInSmallestUnit = refund.Amount
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
