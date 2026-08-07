using Planura.Core.Domain.Enums;

namespace Planura.Core.Application.Models.AdminBooking
{
    public class AdminBookingDto
    {
        public long Id { get; set; }
        public DateOnly EventDate { get; set; }
        public decimal? AgreedPrice { get; set; }
        
        public BookingStatus Status { get; set; }
        public BookingPaymentStatus PaymentStatus { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DisputeStatus? DisputeStatus { get; set; }
        public string ClientName { get; set; } = null;

        public string VendorName { get; set; } = null;
        public string? PackageName {  get; set; }

        public RefundStatus RefundStatus { get; set; }
        public decimal? CancellationRefundAmount { get; set; }
        public DateTimeOffset? CancellationRequestedAt { get; set; }
    }
}
