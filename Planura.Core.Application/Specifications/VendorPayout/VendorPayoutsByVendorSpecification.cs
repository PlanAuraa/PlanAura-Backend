using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications.VendorPayout;

/// <summary>All manual payouts recorded for one vendor, newest first.</summary>
public class VendorPayoutsByVendorSpecification : BaseSpecification<Domain.Entities.VendorPayout>
{
    public VendorPayoutsByVendorSpecification(long vendorId)
        : base(payout => payout.VendorId == vendorId)
    {
        ApplyOrderByDescending(payout => payout.PayoutDate);
        AddInclude(payout => payout.RecordedByAdmin);
    }
}
