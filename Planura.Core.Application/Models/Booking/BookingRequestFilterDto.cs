using Planura.Core.Domain.Enums;

namespace Planura.Core.Application.Models;

public class BookingRequestFilterDto
{
    public BookingStatus? Status { get; set; }

    /// <summary>
    /// Filters on the booking's payment state, independent of <see cref="Status"/>. A refund is
    /// tracked here (not on BookingStatus), so this is the only way to list refunded bookings -
    /// which keep whatever booking status they had when the refund was issued.
    /// </summary>
    public BookingPaymentStatus? PaymentStatus { get; set; }

    /// <summary>
    /// Drops refunded bookings from the result. Because a refund leaves BookingStatus untouched, a
    /// refunded booking would otherwise still surface under its original status (an Accepted booking
    /// that was refunded shows up when filtering on Accepted). Set this when the caller wants a
    /// status filter to mean "only bookings still in that state".
    /// </summary>
    public bool ExcludeRefunded { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
