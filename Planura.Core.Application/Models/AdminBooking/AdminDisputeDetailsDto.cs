using Planura.Core.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planura.Core.Application.Models.AdminBooking
{
    public class AdminDisputeDetailsDto
    {
        public long BookingId { get; set; }
        public DateOnly EventDate { get; set; }
        public decimal? AgreedPrice { get; set; }
        public int? GuestCount { get; set; }
        public BookingStatus BookingStatus { get; set; }
        public BookingPaymentStatus PaymentStatus { get; set; }
        public long ClientId { get; set; }

        public string ClientName { get; set; }
        public long VendorId { get; set; }
        public string VendorName { get; set; }
        public DisputeStatus? DisputeStatus { get; set; }
        public DateTimeOffset? DisputedAt { get; set; }

        /// <summary>
        /// The actual reason the dispute was raised, extracted from the "Dispute raised: ..." entry
        /// BookingService.FlagDisputeAsync writes to BookingStatusHistory.Notes. ClientMessage/
        /// VendorResponse below predate the dispute (they're the original booking request's message/
        /// vendor reply) and must not be mistaken for it - see AdminDashboardImplementationReview.md Bug #2.
        /// </summary>
        public string? DisputeReason { get; set; }
        public string? ResolutionNotes { get; set; }
        public long? ResolvedByAdminId { get; set; }
        public string? ResolvedByAdminName { get; set; }
        public DateTimeOffset? ResolvedAt { get; set; }
        public string? ClientMessage { get; set; }
        public string? VendorResponse { get; set; }
    }
}
