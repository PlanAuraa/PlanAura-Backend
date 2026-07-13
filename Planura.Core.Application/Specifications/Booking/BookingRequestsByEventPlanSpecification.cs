using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class BookingRequestsByEventPlanSpecification : BaseSpecification<BookingRequest>
{
    public BookingRequestsByEventPlanSpecification(long eventPlanId)
        : base(booking => booking.EventPlanId == eventPlanId)
    {
    }
}
