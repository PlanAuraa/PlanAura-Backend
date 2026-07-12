using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications.VendorVerification
{
    public class VendorVerificationHistoryByVendorSpecification
        : BaseSpecification<Planura.Core.Domain.Entities.VendorVerificationHistory>
    {
        public VendorVerificationHistoryByVendorSpecification(long vendorId)
            : base(h => h.VendorVerification.VendorId == vendorId)
        {
            AddInclude(h => h.ChangedByAdmin);

            ApplyOrderByDescending(h => h.ChangedAt);
        }
    }
}
