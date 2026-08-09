using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

/// <summary>
/// Payments stuck in the transient RemainderCharging claim state past the timeout (RemainderChargingSince at
/// or before the cutoff = now − RemainderChargingTimeoutMinutes) — e.g. an on-session SCA the client never
/// completed. The grace-expiry job reclaims these back to RemainderFailed so they are never permanently stuck.
/// </summary>
public class StuckRemainderChargingPaymentsSpecification : BaseSpecification<Payment>
{
    public StuckRemainderChargingPaymentsSpecification(DateTimeOffset chargingCutoff)
        : base(payment => payment.Status == PaymentStatus.RemainderCharging
            && payment.RemainderChargingSince != null
            && payment.RemainderChargingSince <= chargingCutoff)
    {
    }
}
