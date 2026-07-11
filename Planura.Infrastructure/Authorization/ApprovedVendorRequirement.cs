using Microsoft.AspNetCore.Authorization;

namespace Planura.Infrastructure.Authorization;

/// <summary>
/// Requires the caller to be a vendor whose current verification status is approved
/// (verified or trusted). Checked against the database on every request so that admin
/// approval takes effect immediately without the vendor needing to log in again.
/// </summary>
public class ApprovedVendorRequirement : IAuthorizationRequirement
{
}
