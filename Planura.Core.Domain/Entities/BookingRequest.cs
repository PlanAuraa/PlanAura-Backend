using Planura.Core.Domain.Enums;

namespace Planura.Core.Domain.Entities;

public class BookingRequest
{
    public long Id { get; set; }
    public long EventPlanId { get; set; }
    public long ClientId { get; set; }
    public long VendorId { get; set; }
    public long? VendorPackageId { get; set; }
    public DateOnly EventDate { get; set; }
    public int? GuestCount { get; set; }
    public decimal? AgreedPrice { get; set; }
    public string? ClientMessage { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public BookingPaymentStatus PaymentStatus { get; set; } = BookingPaymentStatus.Unpaid;
    public string? VendorResponse { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? PaymentReminderSentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // AI-generated Event Booking Contract. Generated when the client reaches the payment step and
    // copied onto the booking at creation (client), then reviewed as-is by the vendor before accept.
    public string? ContractId { get; set; }
    public string? ContractDocumentUrl { get; set; }
    public DateTimeOffset? ContractGeneratedAt { get; set; }

    // When each party explicitly accepted the Booking Agreement above (the consent gate that
    // precedes their existing action): the client before "Confirm & Book", the vendor before "Accept".
    public DateTimeOffset? ClientAgreedAt { get; set; }
    public DateTimeOffset? VendorAgreedAt { get; set; }

    public DisputeStatus? DisputeStatus { get; set; }
    public DateTimeOffset? DisputedAt { get; set; }
    public long? DisputedByUserId { get; set; }
    public string? ResolutionNotes { get; set; }
    public long? ResolvedByAdminId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    // Set when the event's slot has ended and the booking moves to AwaitingConfirmation — the
    // clock the auto-confirm grace window (BookingOptions.AutoConfirmAfterDays) counts from.
    public DateTimeOffset? AwaitingConfirmationSince { get; set; }

    // Client-requested cancellation of an Accepted booking, pending admin review (see
    // BookingStatus.CancellationRequested). RefundPercent/Amount are the policy-computed estimate
    // at request time; the admin can adjust the final amount on approval.
    public string? CancellationReason { get; set; }
    public DateTimeOffset? CancellationRequestedAt { get; set; }
    public string? CancellationReviewNotes { get; set; }
    // When the admin approved or rejected the request (Cancelled/Accepted resolves the same moment
    // CancelledAt is set on approval, but rejection has no other timestamp of its own — this covers
    // both cases uniformly for the client-facing booking timeline).
    public DateTimeOffset? CancellationReviewedAt { get; set; }
    public decimal? CancellationRefundPercent { get; set; }
    public decimal? CancellationRefundAmount { get; set; }
    public RefundStatus RefundStatus { get; set; } = RefundStatus.None;

    public EventPlan EventPlan { get; set; } = null!;
    public Client Client { get; set; } = null!;
    public Vendor Vendor { get; set; } = null!;
    public VendorPackage? VendorPackage { get; set; }
    public ApplicationUser? DisputedByUser { get; set; }
    public ApplicationUser? ResolvedByAdmin { get; set; }
    public ICollection<VendorAvailability> VendorAvailability { get; set; } = new List<VendorAvailability>();
    public ICollection<BookingStatusHistory> StatusHistory { get; set; } = new List<BookingStatusHistory>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<BookingChatMessage> ChatMessages { get; set; } = new List<BookingChatMessage>();
    public Review? Review { get; set; }
}
