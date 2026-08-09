using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class BookingRequestByIdSpecification : BaseSpecification<BookingRequest>
{
    public BookingRequestByIdSpecification(long id) : base(booking => booking.Id == id)
    {
        AddInclude(booking => booking.Client.User);
        AddInclude(booking => booking.VendorAvailability);
        // Needed for the payment summary on the DTO: what was authorized vs actually captured.
        AddInclude(booking => booking.Payments);
    }
}
