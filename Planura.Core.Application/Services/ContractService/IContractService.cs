using Planura.Core.Application.Models;

namespace Planura.Core.Application.Services.Contract;

public interface IContractService
{
    /// <summary>Generates the Event Booking Contract between a Client and a Vendor.</summary>
    Task<ContractDocumentDto> GenerateBookingContractAsync(GenerateContractDto dto, CancellationToken cancellationToken = default);

    /// <summary>Generates the Vendor Partnership Agreement between Planura and a Vendor. No Client is party to this document.</summary>
    Task<ContractDocumentDto> GenerateVendorPartnershipContractAsync(GenerateVendorPartnershipDto dto, CancellationToken cancellationToken = default);
}
