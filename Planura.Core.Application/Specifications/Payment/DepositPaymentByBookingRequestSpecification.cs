using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

/// <summary>
/// The deposit Payment for a booking (there is one per deposit-path booking), regardless of its current
/// remainder state. Used by the client "pay remainder now" flow to inspect the payment and decide
/// eligibility, then charge the outstanding balance.
/// </summary>
public class DepositPaymentByBookingRequestSpecification : BaseSpecification<Payment>
{
    public DepositPaymentByBookingRequestSpecification(long bookingRequestId)
        : base(payment => payment.BookingRequestId == bookingRequestId && payment.IsDeposit)
    {
    }
}
