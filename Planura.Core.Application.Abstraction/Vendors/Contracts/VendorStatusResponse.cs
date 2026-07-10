using Planura.Core.Domain.Enums;

namespace Planura.Core.Application.Abstraction.Vendors.Contracts
{
    public class VendorStatusResponse
    {
        public VerificationStatus Status { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? LatestDecisionReason { get; set; }
    }
}
