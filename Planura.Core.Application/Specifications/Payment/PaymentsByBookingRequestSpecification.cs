using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

/// <summary>The full payment timeline for one booking (oldest first) — for the admin
/// payment-detail view. Under today's full-payment flow this is usually a single row, but the
/// Payment<->BookingRequest relationship is one-to-many, so this returns all of them.</summary>
public class PaymentsByBookingRequestSpecification : BaseSpecification<Payment>
{
    public PaymentsByBookingRequestSpecification(long bookingRequestId)
        : base(payment => payment.BookingRequestId == bookingRequestId)
    {
        ApplyOrderBy(payment => payment.CreatedAt);
    }
}
