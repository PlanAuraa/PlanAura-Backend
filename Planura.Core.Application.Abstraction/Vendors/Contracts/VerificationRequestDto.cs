using Planura.Core.Domain.Enums;

namespace Planura.Core.Application.Abstraction.Vendors.Contracts
{
    public class VerificationRequestDto
    {
        public Guid Id { get; set; }
        public VerificationStatus Status { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? DecisionReason { get; set; }
        public List<VendorDocumentMetadataDto> Documents { get; set; } = new();
    }
}
