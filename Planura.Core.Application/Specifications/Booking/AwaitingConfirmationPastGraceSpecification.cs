using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

/// <summary>
/// Bookings sitting in AwaitingConfirmation whose grace window has elapsed with no open dispute —
/// the auto-confirm pass of BookingAutoCompleteJob. <paramref name="cutoff"/> is
/// now - BookingOptions.AutoConfirmAfterDays, computed by the caller so this spec stays a pure
/// filter (matches AcceptedPaidBookingsPastEndAtSpecification's convention of taking "now").
/// </summary>
public class AwaitingConfirmationPastGraceSpecification : BaseSpecification<BookingRequest>
{
    public AwaitingConfirmationPastGraceSpecification(DateTimeOffset cutoff)
        : base(booking =>
            booking.Status == BookingStatus.AwaitingConfirmation &&
            booking.AwaitingConfirmationSince != null &&
            booking.AwaitingConfirmationSince <= cutoff &&
            booking.DisputeStatus != DisputeStatus.Open)
    {
    }
}
