using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

/// <summary>The full status-change audit trail for one booking (oldest first) — for the admin
/// payment-detail view, showing who changed what and when (including cancellation
/// request/approve/reject and completion transitions).</summary>
public class BookingStatusHistoryByBookingRequestSpecification : BaseSpecification<BookingStatusHistory>
{
    public BookingStatusHistoryByBookingRequestSpecification(long bookingRequestId)
        : base(history => history.BookingRequestId == bookingRequestId)
    {
        AddInclude(history => history.ChangedByUser!);
        ApplyOrderBy(history => history.ChangedAt);
    }
}
