using Planura.Core.Application.Models;

namespace Planura.Core.Application.Services;

public interface IBookingService
{
    Task<BookingRequestDto> CreateBookingRequestAsync(long clientUserId, CreateBookingRequestDto dto);
    Task<BookingRequestDto> CancelBookingRequestAsync(long bookingRequestId, long clientUserId);
    Task<BookingRequestDto> GetBookingRequestAsync(long bookingRequestId, long clientUserId);
    Task<PagedResult<BookingRequestDto>> ListMyBookingRequestsAsync(long clientUserId, BookingRequestFilterDto filter);
    Task<BookingRequestDto> FlagDisputeAsync(long bookingRequestId, long userId, string reason);
}
