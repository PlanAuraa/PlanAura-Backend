using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class BookingChatMessagesByBookingRequestSpecification : BaseSpecification<BookingChatMessage>
{
    public BookingChatMessagesByBookingRequestSpecification(long bookingRequestId, long? afterId = null)
        : base(message => message.BookingRequestId == bookingRequestId
            && (afterId == null || message.Id > afterId))
    {
        ApplyOrderBy(message => message.CreatedAt);
    }
}
