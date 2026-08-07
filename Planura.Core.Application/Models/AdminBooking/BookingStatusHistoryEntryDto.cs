namespace Planura.Core.Application.Models.AdminBooking
{
    public class BookingStatusHistoryEntryDto
    {
        public string? PreviousStatus { get; set; }
        public string NewStatus { get; set; } = null!;
        public long? ChangedByUserId { get; set; }
        public string? ChangedByName { get; set; }
        public string? Notes { get; set; }
        public DateTimeOffset ChangedAt { get; set; }
    }
}
