using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Repositories;
using Planura.Core.Domain.Entities;


namespace Planura.Core.Application.Specifications.AdminDashboard
{
    public class PendingVendorCountSpecification :
 BaseSpecification<Planura.Core.Domain.Entities.VendorVerification>
{
    public PendingVendorCountSpecification()
        : base(v =>
            v.IsCurrent &&
            v.Status == VerificationStatus.Pending)
        { }
    
}
}