namespace Planura.Core.Domain.Enums;

/// <summary>
/// Tracks a booking's cancellation-refund lifecycle, separate from BookingPaymentStatus (which
/// only distinguishes Unpaid/Paid/Refunded on the payment itself). None means no cancellation has
/// ever been requested; PendingReview means a refund estimate exists awaiting admin decision.
/// </summary>
public enum RefundStatus
{
    None = 1,
    PendingReview = 2,
    Processed = 3,
    Rejected = 4
}
