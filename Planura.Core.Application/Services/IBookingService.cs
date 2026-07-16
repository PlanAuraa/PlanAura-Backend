using Planura.Core.Application.Models;

namespace Planura.Core.Application.Services;

public interface IBookingService
{
    Task<BookingRequestDto> CreateBookingRequestAsync(long clientUserId, CreateBookingRequestDto dto);
    Task<BookingRequestDto> CancelBookingRequestAsync(long bookingRequestId, long clientUserId);
    Task<BookingRequestDto> GetBookingRequestAsync(long bookingRequestId, long clientUserId);
    Task<PagedResult<BookingRequestDto>> ListMyBookingRequestsAsync(long clientUserId, BookingRequestFilterDto filter);
    Task<BookingRequestDto> FlagDisputeAsync(long bookingRequestId, long userId, string reason);
    Task<BookingRequestDto> AcceptBookingRequestAsync(long bookingRequestId, long vendorUserId);
    Task<BookingRequestDto> RejectBookingRequestAsync(long bookingRequestId, long vendorUserId, string? reason);
    Task<PagedResult<BookingRequestDto>> ListVendorBookingRequestsAsync(long vendorUserId, BookingRequestFilterDto filter);
    Task<BookingRequestDto> GetVendorBookingRequestAsync(long bookingRequestId, long vendorUserId);
}
