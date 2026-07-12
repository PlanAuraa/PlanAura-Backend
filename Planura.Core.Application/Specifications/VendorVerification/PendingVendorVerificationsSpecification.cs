using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class PendingVendorVerificationsSpecification : BaseSpecification<Planura.Core.Domain.Entities.VendorVerification>
{
    public PendingVendorVerificationsSpecification()
        : base(v =>
            v.IsCurrent &&
            v.Status == VerificationStatus.Pending)
    {
        AddInclude(v => v.Vendor);
        AddInclude(v => v.Vendor.User);
        AddInclude(v => v.Vendor.Category);

        ApplyOrderByDescending(v => v.SubmittedAt);
    }
}