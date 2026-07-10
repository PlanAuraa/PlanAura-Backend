namespace Planura.Core.Domain.Entities;

public class BookingStatusHistory
{
    public long Id { get; set; }
    public long BookingRequestId { get; set; }
    public string? PreviousStatus { get; set; }
    public string NewStatus { get; set; } = null!;
    public long? ChangedByUserId { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;

    public BookingRequest BookingRequest { get; set; } = null!;
    public ApplicationUser? ChangedByUser { get; set; }
}
