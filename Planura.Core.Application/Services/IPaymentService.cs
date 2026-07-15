using Planura.Core.Application.Models;

namespace Planura.Core.Application.Services;

public interface IPaymentService
{
    Task<PaymentOptionsDto> GetPaymentOptionsAsync(long bookingRequestId, long clientUserId);
    Task<InitiatePaymentResultDto> InitiatePaymentAsync(long bookingRequestId, long clientUserId, InitiatePaymentDto dto);
    Task HandleStripeWebhookAsync(string rawJson, string stripeSignatureHeader);
    Task<PagedResult<PaymentDto>> ListMyTransactionsAsync(long userId, TransactionsFilterDto filter);
}
