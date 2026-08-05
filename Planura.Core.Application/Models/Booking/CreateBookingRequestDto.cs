namespace Planura.Core.Application.Models;

public class CreateBookingRequestDto
{
    public long EventPlanId { get; set; }
    public long AvailabilityId { get; set; }
    public long? VendorPackageId { get; set; }
    public int? GuestCount { get; set; }
    public string? ClientMessage { get; set; }

    /// <summary>Stripe payment method id (pm_...) collected client-side via Stripe Elements, used to authorize the hold.</summary>
    public string PaymentMethodId { get; set; } = null!;

    /// <summary>Client-generated id for this submission, reused as the Stripe idempotency key to dedupe retried submits.</summary>
    public string RequestId { get; set; } = null!;

    /// <summary>
    /// Opaque token returned by the agreement-preview step, identifying the Booking Agreement the
    /// client reviewed. Redeemed once here to copy that exact contract onto the new booking.
    /// </summary>
    public string AgreementToken { get; set; } = null!;

    /// <summary>Must be true — the client ticked "I have read and agree to the Booking Agreement" before confirming.</summary>
    public bool AgreementAccepted { get; set; }
}
