using Planura.Core.Application.Models;
using Planura.Core.Application.Models.AdminPayment;

namespace Planura.Core.Application.Services.AdminPayment
{
    public interface IAdminPaymentService
    {
        Task<PagedResult<AdminPaymentListItemDto>> ListPaymentsAsync(AdminPaymentFilterDto filter);

        Task<AdminPaymentSummaryDto> GetSummaryAsync();

        /// <summary>Admin-initiated refund of a captured (Completed) payment. Throws BadRequestExeption
        /// if the payment isn't in a refundable state.</summary>
        Task<AdminPaymentListItemDto> RefundPaymentAsync(long paymentId, long adminId, RefundPaymentDto dto);
    }
}
