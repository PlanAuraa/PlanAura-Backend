using Planura.Core.Domain.Enums;

namespace Planura.Core.Application.Models.AdminBooking
{
    /// <summary>One Payment row on a booking's timeline (usually just one, under today's full-payment
    /// flow) — everything an admin needs to see what happened to the money.</summary>
    public class PaymentTimelineEntryDto
    {
        public long PaymentId { get; set; }
        public decimal Amount { get; set; }
        public PaymentStatus Status { get; set; }
        public string? PaymentMethod { get; set; }
        public string? GatewayReference { get; set; }
        public DateTimeOffset? AuthorizedAt { get; set; }
        public DateTimeOffset? PaidAt { get; set; }
        public DateTimeOffset? CancelledAt { get; set; }
        public DateTimeOffset? RefundedAt { get; set; }
        public decimal? RefundedAmount { get; set; }
        public string? RefundReason { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
