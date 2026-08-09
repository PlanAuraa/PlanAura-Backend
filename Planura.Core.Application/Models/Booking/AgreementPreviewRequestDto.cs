namespace Planura.Core.Application.Models;

/// <summary>
/// The fixed booking details the client has chosen by the time they reach the payment step. The
/// server generates the Booking Agreement from these (deriving price from the package, its source
/// of truth) so the client can review it before "Confirm &amp; Book". Mirrors the create payload,
/// minus payment/idempotency fields.
/// </summary>
public class AgreementPreviewRequestDto
{
    public long EventPlanId { get; set; }
    public long AvailabilityId { get; set; }
    public long? VendorPackageId { get; set; }
    public int? GuestCount { get; set; }
    public string? ClientMessage { get; set; }

    /// <summary>
    /// The client's own requirements for this booking. Optional, and the strongest single source of
    /// per-contract variation: what this client asks for is what makes their agreement theirs.
    /// </summary>
    public ClientRequirementsDto? Requirements { get; set; }

    public IReadOnlyList<string> BuildClientRequirements() =>
        Requirements?.ToRequirementLines() ?? Array.Empty<string>();
}
