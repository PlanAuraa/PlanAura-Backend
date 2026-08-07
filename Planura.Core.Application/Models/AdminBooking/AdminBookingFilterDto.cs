using Planura.Core.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planura.Core.Application.Models.AdminBooking
{
    public class AdminBookingFilterDto
    {
        public string? Search {  get; set; }

        public BookingStatus? Status {  get; set; }

        public BookingPaymentStatus? PaymentStatus {  get; set; }

        public DisputeStatus? DisputeStatus {  get; set; }

        public RefundStatus? RefundStatus { get; set; }

        public DateOnly? FromDate {  get; set; }

        public DateOnly? ToDate {  get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 20;
    }
}
