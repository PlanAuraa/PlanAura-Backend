namespace Planura.Core.Application.Models;

/// <summary>
/// Result of a client "pay remainder now" (on-session) request. When the charge succeeds outright,
/// <see cref="RequiresAction"/> is false and the booking is already fully paid. When SCA is required,
/// <see cref="RequiresAction"/> is true and the frontend must complete authentication with
/// <see cref="ClientSecret"/> (e.g. Stripe.js confirmCardPayment); success is then finalized via webhook.
/// </summary>
public class PayRemainderResultDto
{
    public string Status { get; set; } = null!;      // Stripe PaymentIntent status: "succeeded" | "requires_action" | ...
    public string PaymentIntentId { get; set; } = null!;
    public string? ClientSecret { get; set; }         // present when RequiresAction is true
    public bool RequiresAction { get; set; }
}
