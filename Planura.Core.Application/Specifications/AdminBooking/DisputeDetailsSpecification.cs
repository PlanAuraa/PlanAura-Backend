using Planura.Core.Domain.Repositories;
using Planura.Core.Domain.Entities;

namespace Planura.Core.Application.Specifications.AdminBooking
{
    public class DisputeDetailsSpecification : BaseSpecification<BookingRequest>
    {
        public DisputeDetailsSpecification(long bookingId)
            : base(d => d.Id == bookingId &&
    d.DisputeStatus != null)
        {
            AddInclude(d => d.Client.User);
            AddInclude(d => d.Vendor.User);
            AddInclude(d => d.StatusHistory);
            AddInclude(d => d.ResolvedByAdmin!);
        }
    }
}
