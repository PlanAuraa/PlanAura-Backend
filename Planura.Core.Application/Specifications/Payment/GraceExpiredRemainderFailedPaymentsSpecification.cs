using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

/// <summary>
/// Deposit payments whose remainder failed and whose grace window has now elapsed (RemainderFailedAt at or
/// before the cutoff = now − GracePeriodDays). The grace-expiry job routes these bookings to admin
/// cancellation review. A payment that leaves RemainderFailed (client paid → RemainderCharging/FullyPaid) is
/// no longer selected, so a client paying at the last moment is never auto-cancelled.
/// </summary>
public class GraceExpiredRemainderFailedPaymentsSpecification : BaseSpecification<Payment>
{
    public GraceExpiredRemainderFailedPaymentsSpecification(DateTimeOffset graceCutoff)
        : base(payment => payment.Status == PaymentStatus.RemainderFailed
            && payment.RemainderFailedAt != null
            && payment.RemainderFailedAt <= graceCutoff)
    {
    }
}
