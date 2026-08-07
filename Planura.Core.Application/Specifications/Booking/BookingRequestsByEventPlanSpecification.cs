using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class BookingRequestsByEventPlanSpecification : BaseSpecification<BookingRequest>
{
    public BookingRequestsByEventPlanSpecification(long eventPlanId)
        : base(booking => booking.EventPlanId == eventPlanId)
    {
        // Needed by EventPlanService's budget/checklist computation (checklist satisfaction reads
        // the vendor's CategoryId); a no-op extra include for the delete-path count check.
        AddInclude(booking => booking.Vendor);
    }
}
