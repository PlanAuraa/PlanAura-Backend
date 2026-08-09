using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

/// <summary>
/// Deposit-path bookings whose remainder is due to be charged: Accepted, still resting at the
/// deposit-paid (remainder outstanding) state, and whose event is within RemainderChargeLeadDays of now
/// (the slot's StartAt is at or before the lead cutoff). The remainder-charge job (Phase 2) charges these
/// off-session. Full-payment bookings (PaymentStatus.Paid) and already fully-paid/failed deposit bookings
/// are excluded by the DepositPaid filter.
/// </summary>
public class RemainderDueBookingsWithinLeadSpecification : BaseSpecification<BookingRequest>
{
    public RemainderDueBookingsWithinLeadSpecification(DateTimeOffset leadCutoff)
        : base(booking =>
            booking.Status == BookingStatus.Accepted &&
            booking.PaymentStatus == BookingPaymentStatus.DepositPaid &&
            booking.VendorAvailability.Any(availability => availability.StartAt <= leadCutoff))
    {
    }
}
