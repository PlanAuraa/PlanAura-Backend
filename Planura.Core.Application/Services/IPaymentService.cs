using Planura.Core.Application.Models;

namespace Planura.Core.Application.Services;

public interface IPaymentService
{
    Task HandleStripeWebhookAsync(string rawJson, string stripeSignatureHeader);
    Task<PagedResult<PaymentDto>> ListMyTransactionsAsync(long userId, TransactionsFilterDto filter);
}
