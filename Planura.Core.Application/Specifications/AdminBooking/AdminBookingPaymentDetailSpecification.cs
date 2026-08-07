using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications.AdminBooking
{
    /// <summary>Loads one booking with the navigations AdminBookingService.GetBookingPaymentDetailAsync
    /// needs to resolve client contact details and vendor name.</summary>
    public class AdminBookingPaymentDetailSpecification : BaseSpecification<BookingRequest>
    {
        public AdminBookingPaymentDetailSpecification(long bookingId)
            : base(b => b.Id == bookingId)
        {
            AddInclude(b => b.Client.User);
            AddInclude(b => b.Vendor);
        }
    }
}
