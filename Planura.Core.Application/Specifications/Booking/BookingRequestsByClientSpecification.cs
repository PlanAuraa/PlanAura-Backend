using System.Linq.Expressions;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class BookingRequestsByClientSpecification : BaseSpecification<BookingRequest>
{
    public BookingRequestsByClientSpecification(long clientId, BookingStatus? status, int? skip, int? take)
        : base(BuildCriteria(clientId, status))
    {
        AddInclude(booking => booking.Client.User);
        AddInclude(booking => booking.Review!);
        AddInclude(booking => booking.VendorAvailability);
        ApplyOrderByDescending(booking => booking.CreatedAt);

        if (skip is not null && take is not null)
        {
            ApplyPaging(skip.Value, take.Value);
        }
    }

    private static Expression<Func<BookingRequest, bool>> BuildCriteria(long clientId, BookingStatus? status)
    {
        return status is null
            ? booking => booking.ClientId == clientId
            : booking => booking.ClientId == clientId && booking.Status == status.Value;
    }
}
