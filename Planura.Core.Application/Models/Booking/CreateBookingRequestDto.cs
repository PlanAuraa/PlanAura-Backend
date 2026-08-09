namespace Planura.Core.Application.Models;

public class CreateBookingRequestDto
{
    public long EventPlanId { get; set; }
    public long AvailabilityId { get; set; }
    public long? VendorPackageId { get; set; }
    public int? GuestCount { get; set; }
    public string? ClientMessage { get; set; }

    /// <summary>
    /// The client's own requirements for this booking, as reviewed in the Booking Agreement. Mirrors
    /// <see cref="AgreementPreviewRequestDto.Requirements"/> so the booking that is created records the
    /// same requirements the generated contract was drafted from.
    /// </summary>
    public ClientRequirementsDto? Requirements { get; set; }

    public IReadOnlyList<string> BuildClientRequirements() =>
        Requirements?.ToRequirementLines() ?? Array.Empty<string>();

    /// <summary>
    /// The client's note with their stated requirements appended, for persistence on the booking.
    /// Stored together in the existing free-text field so the vendor sees, in one place, exactly what
    /// they are accepting - the same requirements that appear in the contract.
    /// </summary>
    public string? BuildPersistedClientMessage()
    {
        var requirements = BuildClientRequirements();
        if (requirements.Count == 0)
        {
            return ClientMessage;
        }

        var block = "Client requirements:" + Environment.NewLine +
                    string.Join(Environment.NewLine, requirements.Select(r => $"- {r}"));

        return string.IsNullOrWhiteSpace(ClientMessage)
            ? block
            : ClientMessage.Trim() + Environment.NewLine + Environment.NewLine + block;
    }

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
