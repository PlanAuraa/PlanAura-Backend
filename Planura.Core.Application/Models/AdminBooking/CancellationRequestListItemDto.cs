namespace Planura.Core.Application.Models.AdminBooking
{
    /// <summary>One row of the admin "Cancellation Requests" queue — everything needed to triage and
    /// decide without opening the full booking, including contact details so the admin can reach out
    /// proactively.</summary>
    public class CancellationRequestListItemDto
    {
        public long BookingId { get; set; }

        public long ClientId { get; set; }
        public string? ClientName { get; set; }
        public string? ClientEmail { get; set; }
        public string? ClientPhone { get; set; }

        public long VendorId { get; set; }
        public string? VendorName { get; set; }

        public DateOnly EventDate { get; set; }
        public decimal? AgreedPrice { get; set; }

        public string? CancellationReason { get; set; }
        public DateTimeOffset? CancellationRequestedAt { get; set; }
        public decimal? CancellationRefundPercent { get; set; }
        public decimal? CancellationRefundAmount { get; set; }
    }
}
