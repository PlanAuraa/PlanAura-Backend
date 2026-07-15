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

    public DisputeStatus? DisputeStatus { get; set; }
    public DateTimeOffset? DisputedAt { get; set; }
    public long? DisputedByUserId { get; set; }
    public string? ResolutionNotes { get; set; }
    public long? ResolvedByAdminId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    public EventPlan EventPlan { get; set; } = null!;
    public Client Client { get; set; } = null!;
    public Vendor Vendor { get; set; } = null!;
    public VendorPackage? VendorPackage { get; set; }
    public ApplicationUser? DisputedByUser { get; set; }
    public ApplicationUser? ResolvedByAdmin { get; set; }
    public ICollection<VendorAvailability> VendorAvailability { get; set; } = new List<VendorAvailability>();
    public ICollection<BookingStatusHistory> StatusHistory { get; set; } = new List<BookingStatusHistory>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public Review? Review { get; set; }
}
