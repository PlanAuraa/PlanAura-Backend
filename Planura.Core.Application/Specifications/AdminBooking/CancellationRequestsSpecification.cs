using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications.AdminBooking
{
    /// <summary>The admin "Cancellation Requests" queue: bookings awaiting an approve/reject decision,
    /// oldest request first so old requests don't get buried under new ones.</summary>
    public class CancellationRequestsSpecification : BaseSpecification<BookingRequest>
    {
        public CancellationRequestsSpecification()
            : base(b => b.Status == BookingStatus.CancellationRequested)
        {
            AddInclude(b => b.Client.User);
            AddInclude(b => b.Vendor);

            ApplyOrderBy(b => b.CancellationRequestedAt!);
        }
    }
}
