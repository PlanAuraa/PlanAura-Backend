using Planura.Core.Domain.Enums;

namespace Planura.Core.Application.Models.AdminBooking
{
    /// <summary>Full payment-lifecycle visibility for one booking, for the admin dashboard: totals,
    /// refund/cancellation state, the raw Payment timeline, the status-change audit trail, and client
    /// contact details so the admin can proactively reach out.</summary>
    public class AdminBookingPaymentDetailDto
    {
        public long BookingId { get; set; }
        public BookingStatus Status { get; set; }
        public DateOnly EventDate { get; set; }

        public long ClientId { get; set; }
        public string? ClientName { get; set; }
        public string? ClientEmail { get; set; }
        public string? ClientPhone { get; set; }

        public long VendorId { get; set; }
        public string? VendorName { get; set; }

        public decimal? TotalAmount { get; set; }

        // Derived from the latest Payment row's own captured-amount fields (Payment.GetAmountCaptured()),
        // never from PaymentStatus below - that enum is only a coarse cache. See Payment.AmountCapturedExpression.
        public decimal AmountPaid { get; set; }
        public decimal RemainingAmount { get; set; }
        public decimal RefundedAmount { get; set; }

        public BookingPaymentStatus PaymentStatus { get; set; }

        public RefundStatus RefundStatus { get; set; }
        public string? CancellationReason { get; set; }
        public DateTimeOffset? CancellationRequestedAt { get; set; }
        public string? CancellationReviewNotes { get; set; }
        public decimal? CancellationRefundPercent { get; set; }
        public decimal? CancellationRefundAmount { get; set; }
        public DateTimeOffset? CancelledAt { get; set; }

        public DisputeStatus? DisputeStatus { get; set; }
        public DateTimeOffset? DisputedAt { get; set; }
        public string? ResolutionNotes { get; set; }

        public List<PaymentTimelineEntryDto> PaymentTimeline { get; set; } = [];
        public List<BookingStatusHistoryEntryDto> StatusHistory { get; set; } = [];
    }
}
