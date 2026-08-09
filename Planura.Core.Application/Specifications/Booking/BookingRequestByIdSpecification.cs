using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class BookingRequestByIdSpecification : BaseSpecification<BookingRequest>
{
    public BookingRequestByIdSpecification(long id) : base(booking => booking.Id == id)
    {
        AddInclude(booking => booking.Client.User);
        AddInclude(booking => booking.VendorAvailability);
        AddInclude(booking => booking.Payments); // deposit split for the client cancel warning
    }
}
