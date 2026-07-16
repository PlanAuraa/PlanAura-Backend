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
}
