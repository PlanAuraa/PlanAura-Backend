using Planura.Core.Application.Models;

namespace Planura.Core.Application.Services.Contract;

public interface IContractService
{
    /// <summary>
    /// Generates the Event Booking Contract for one specific, fully-resolved transaction. This is the
    /// path callers with real booking data should use: the richer the context, the more the contract
    /// is grounded in the actual deal rather than in generic terms.
    /// </summary>
    Task<ContractDocumentDto> GenerateBookingContractAsync(
        ContractGenerationContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates the Event Booking Contract from a caller-supplied summary of the deal. Retained for
    /// the direct <c>POST /api/contracts/generate</c> endpoint, where no booking exists to resolve;
    /// internally this is mapped onto a <see cref="ContractGenerationContext"/> and follows exactly
    /// the same pipeline, so it carries no separate prompt or template.
    /// </summary>
    Task<ContractDocumentDto> GenerateBookingContractAsync(GenerateContractDto dto, CancellationToken cancellationToken = default);

    /// <summary>Generates the Vendor Partnership Agreement between Planura and a Vendor. No Client is party to this document.</summary>
    Task<ContractDocumentDto> GenerateVendorPartnershipContractAsync(GenerateVendorPartnershipDto dto, CancellationToken cancellationToken = default);
}
