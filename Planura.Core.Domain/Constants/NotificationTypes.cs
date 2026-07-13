namespace Planura.Core.Domain.Constants;

/// <summary>
/// Canonical notification type values, stored as a plain string on
/// <see cref="Entities.Notification.Type"/> for portability.
/// </summary>
public static class NotificationTypes
{
    public const string VendorSubmitted = "vendor_submitted";
    public const string VendorPendingReview = "vendor_pending_review";
    public const string VendorApproved = "vendor_approved";
    public const string VendorRejected = "vendor_rejected";
    public const string VendorResubmitted = "vendor_resubmitted";
    public const string BookingRequestReceived = "booking_request_received";
    public const string BookingCancelled = "booking_cancelled";
}
