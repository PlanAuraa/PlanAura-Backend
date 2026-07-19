using Planura.Core.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planura.Core.Application.Models.AdminBooking
{
    public class AdminDisputeListItemDto
    {
        public long BookingId { get; set; }
        public long ClientId { get; set; }
        public string? ClientName { get; set; } 
        public long VendorId { get; set; }
        public string? VendorName { get; set; }
        public decimal? AgreedPrice { get; set; }
        public DateOnly EventDate { get; set; }
        public DisputeStatus DisputeStatus { get; set; }
        public DateTimeOffset? DisputedAt { get; set; }
        public BookingStatus BookingStatus { get; set; }
        public BookingPaymentStatus PaymentStatus { get; set; }
    }
}
