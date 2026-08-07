using Planura.Core.Application.Models;
using Planura.Core.Application.Models.AdminBooking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planura.Core.Application.Services.AdminBooking
{
    public interface IAdminBookingService
    {
        Task<IEnumerable<AdminDisputeListItemDto>> GetOpenDisputesAsync();
        Task<AdminDisputeDetailsDto> GetDisputeDetailsAsync(long bookingId);
        Task ResolveDisputeAsync(long bookingId , long adminId , ResolveDisputeDto dto);
        Task<PagedResult<AdminBookingDto>> GetBookingsAsync(AdminBookingFilterDto filter);

        /// <summary>The "Cancellation Requests" queue: bookings awaiting an approve/reject decision,
        /// oldest request first.</summary>
        Task<IEnumerable<CancellationRequestListItemDto>> GetCancellationRequestsAsync();

        /// <summary>Approves a pending cancellation request: refunds via the existing admin-payment
        /// refund path, releases the vendor's slot, and marks the booking Cancelled.</summary>
        Task<AdminBookingDto> ApproveCancellationAsync(long bookingId, long adminId, ApproveCancellationDto dto);

        /// <summary>Rejects a pending cancellation request: the booking returns to Accepted, the
        /// vendor's slot stays booked, and no refund is created.</summary>
        Task<AdminBookingDto> RejectCancellationAsync(long bookingId, long adminId, RejectCancellationDto dto);

        /// <summary>Full payment-lifecycle visibility for one booking: totals, refund/cancellation
        /// state, the Payment timeline, and the status-change audit trail.</summary>
        Task<AdminBookingPaymentDetailDto> GetBookingPaymentDetailAsync(long bookingId);
    }
}
