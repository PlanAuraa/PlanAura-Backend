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
    public string Status { get; set; } = "pending";
    public string? VendorResponse { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public EventPlan EventPlan { get; set; } = null!;
    public Client Client { get; set; } = null!;
    public Vendor Vendor { get; set; } = null!;
    public VendorPackage? VendorPackage { get; set; }
    public ICollection<VendorAvailability> VendorAvailability { get; set; } = new List<VendorAvailability>();
    public ICollection<BookingStatusHistory> StatusHistory { get; set; } = new List<BookingStatusHistory>();
    public Review? Review { get; set; }
}
