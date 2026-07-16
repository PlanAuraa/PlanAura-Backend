using System.Linq.Expressions;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class BookingRequestsByVendorSpecification : BaseSpecification<BookingRequest>
{
    public BookingRequestsByVendorSpecification(long vendorId, BookingStatus? status, int? skip, int? take)
        : base(BuildCriteria(vendorId, status))
    {
        ApplyOrderByDescending(booking => booking.CreatedAt);

        if (skip is not null && take is not null)
        {
            ApplyPaging(skip.Value, take.Value);
        }
    }

    private static Expression<Func<BookingRequest, bool>> BuildCriteria(long vendorId, BookingStatus? status)
    {
        return status is null
            ? booking => booking.VendorId == vendorId
            : booking => booking.VendorId == vendorId && booking.Status == status.Value;
    }
}
