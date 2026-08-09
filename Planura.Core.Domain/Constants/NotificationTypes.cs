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
    public const string BookingAccepted = "booking_accepted";
    public const string BookingRejected = "booking_rejected";
    public const string PaymentSuccessful = "payment_successful";
    public const string PaymentReceived = "payment_received";
    public const string PaymentFailed = "payment_failed";
    public const string BookingRequestExpired = "booking_request_expired";
    public const string BookingCompleted = "booking_completed";
    public const string ContractGenerated = "contract_generated";
    public const string PartnershipAgreementGenerated = "partnership_agreement_generated";
    public const string PartnershipAgreementPendingReview = "partnership_agreement_pending_review";

    public const string DisputeResolved = "dispute_resolved";
    public const string BookingAwaitingConfirmation = "booking_awaiting_confirmation";
    public const string BookingCancellationRequested = "booking_cancellation_requested";
    public const string BookingCancellationApproved = "booking_cancellation_approved";
    public const string BookingCancellationRejected = "booking_cancellation_rejected";

    // Deposit / partial-payment (Phase 3). Remainder charge outcomes and the grace-period lifecycle.
    public const string RemainderPaid = "remainder_paid";
    public const string RemainderFailed = "remainder_failed";
    public const string RemainderPaymentReminder = "remainder_payment_reminder";
}
