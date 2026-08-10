using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

/// <summary>
/// Pending booking requests whose held slot has already ended. Independent of
/// <see cref="HeldExpiredVendorAvailabilitySpecification"/> (the vendor-response-window TTL): a request
/// made shortly before its own event date can outlive its response window without the vendor ever
/// having responded, so a still-Pending booking must also expire once the event itself has passed,
/// not only once the TTL has.
/// </summary>
public class PendingBookingsPastEventSpecification : BaseSpecification<BookingRequest>
{
    public PendingBookingsPastEventSpecification(DateTimeOffset now)
        : base(booking =>
            booking.Status == BookingStatus.Pending &&
            booking.VendorAvailability.Any(availability => availability.EndAt < now))
    {
        AddInclude(booking => booking.VendorAvailability);
    }
}
