namespace Planura.Core.Domain.Constants;

/// <summary>
/// Canonical vendor verification status values. Stored as strings on
/// <see cref="Entities.Vendor.VerificationStatus"/> and
/// <see cref="Entities.VendorVerification.Status"/> for portability, validated at the application layer.
/// </summary>
public static class VerificationStatus
{
    public const string Unverified = "unverified";
    public const string Pending = "pending";
    public const string Verified = "verified";
    public const string Trusted = "trusted";
    public const string Rejected = "rejected";

    /// <summary>An approved vendor is one whose current verification is verified or trusted.</summary>
    public static bool IsApproved(string? status)
        => status is Verified or Trusted;
}
