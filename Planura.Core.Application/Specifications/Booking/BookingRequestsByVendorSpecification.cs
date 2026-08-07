using System.Linq.Expressions;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class BookingRequestsByVendorSpecification : BaseSpecification<BookingRequest>
{
    public BookingRequestsByVendorSpecification(
        long vendorId,
        BookingStatus? status,
        BookingPaymentStatus? paymentStatus,
        bool excludeRefunded,
        int? skip,
        int? take)
        : base(BuildCriteria(vendorId, status, paymentStatus, excludeRefunded))
    {
        AddInclude(booking => booking.Client.User);
        AddInclude(booking => booking.VendorAvailability);
        ApplyOrderByDescending(booking => booking.CreatedAt);

        if (skip is not null && take is not null)
        {
            ApplyPaging(skip.Value, take.Value);
        }
    }

    /// <summary>
    /// Status and payment status are independent axes - a refunded booking keeps its original
    /// BookingStatus (Accepted/Completed/...) - so both predicates are applied when both are given.
    /// <paramref name="excludeRefunded"/> is what makes a status filter exclusive: without it, a
    /// status filter also returns the refunded bookings that still carry that status.
    /// </summary>
    private static Expression<Func<BookingRequest, bool>> BuildCriteria(
        long vendorId,
        BookingStatus? status,
        BookingPaymentStatus? paymentStatus,
        bool excludeRefunded)
    {
        return booking =>
            booking.VendorId == vendorId
            && (status == null || booking.Status == status)
            && (paymentStatus == null || booking.PaymentStatus == paymentStatus)
            && (!excludeRefunded || booking.PaymentStatus != BookingPaymentStatus.Refunded);
    }
}
