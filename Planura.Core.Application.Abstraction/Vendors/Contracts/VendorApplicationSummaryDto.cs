using Planura.Core.Domain.Enums;

namespace Planura.Core.Application.Abstraction.Vendors.Contracts
{
    public class VendorApplicationSummaryDto
    {
        public Guid RequestId { get; set; }
        public Guid VendorProfileId { get; set; }
        public string BusinessName { get; set; } = null!;
        public VendorBusinessType BusinessType { get; set; }
        public DateTime SubmittedAt { get; set; }
    }
}
