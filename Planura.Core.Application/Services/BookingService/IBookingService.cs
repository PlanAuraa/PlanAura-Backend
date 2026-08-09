using Planura.Core.Application.Models;
using Planura.Core.Application.Models.AdminBooking;

namespace Planura.Core.Application.Services.Booking;

public interface IBookingService
{
    /// <summary>
    /// Prices a booking the client has configured but not yet submitted, so checkout can show the
    /// total, the amount due now and the remaining balance before any contract is generated.
    /// </summary>
    Task<BookingPaymentQuoteDto> GetBookingPaymentQuoteAsync(long clientUserId, BookingPaymentQuoteRequestDto dto);

    Task<AgreementPreviewResultDto> PreviewBookingAgreementAsync(long clientUserId, AgreementPreviewRequestDto dto);

    /// <summary>The full-vs-deposit split for a chosen slot + package, computed server-side before payment so
    /// the client can see the deposit breakdown (deposit now, remainder auto-charged, total). Read-only.</summary>
    Task<PaymentPreviewDto> PreviewPaymentAsync(long clientUserId, AgreementPreviewRequestDto dto);
    Task<BookingRequestDto> CreateBookingRequestAsync(long clientUserId, CreateBookingRequestDto dto);
    Task<BookingRequestDto> CancelBookingRequestAsync(long bookingRequestId, long clientUserId);
    Task<BookingRequestDto> ConfirmServiceDeliveredAsync(long bookingRequestId, long clientUserId);
    Task<CancellationQuoteDto> GetCancellationQuoteAsync(long bookingRequestId, long clientUserId);
    Task<BookingRequestDto> RequestCancellationAsync(long bookingRequestId, long clientUserId, string reason);
    Task<BookingRequestDto> GetBookingRequestAsync(long bookingRequestId, long clientUserId);

    /// <summary>Client pays the outstanding remainder on their deposit booking on-session (Phase 3). Charges the
    /// saved card with SCA support; returns whether the frontend must complete authentication.</summary>
    Task<PayRemainderResultDto> PayRemainderAsync(long bookingRequestId, long clientUserId);
    Task<List<BookingStatusHistoryEntryDto>> GetBookingTimelineAsync(long bookingRequestId, long clientUserId);
    Task<PagedResult<BookingRequestDto>> ListMyBookingRequestsAsync(long clientUserId, BookingRequestFilterDto filter);
    Task<BookingRequestDto> FlagDisputeAsync(long bookingRequestId, long userId, string reason);
    Task<BookingRequestDto> AcceptBookingRequestAsync(long bookingRequestId, long vendorUserId, bool agreementAccepted);
    Task<BookingRequestDto> RejectBookingRequestAsync(long bookingRequestId, long vendorUserId, string? reason);
    Task<PagedResult<BookingRequestDto>> ListVendorBookingRequestsAsync(long vendorUserId, BookingRequestFilterDto filter);
    Task<BookingRequestDto> GetVendorBookingRequestAsync(long bookingRequestId, long vendorUserId);
    Task<List<BookingStatusHistoryEntryDto>> GetVendorBookingTimelineAsync(long bookingRequestId, long vendorUserId);
}
