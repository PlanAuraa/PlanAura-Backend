namespace Planura.Core.Application.Models.AdminVendorPayout
{
    /// <summary>
    /// One vendor's settlement picture: what clients have paid in, what's been refunded, and what the
    /// platform still owes that vendor after manual payouts. NetCollected/AmountPayable are computed from
    /// actual Payment rows (via Payment.AmountCapturedExpression) and the VendorPayout ledger - never from
    /// booking status - so they stay correct as bookings move through the deposit/remainder lifecycle.
    /// </summary>
    public class VendorFinancialSummaryDto
    {
        public long VendorId { get; set; }
        public string VendorName { get; set; } = null!;

        public int TotalBookings { get; set; }
        public decimal TotalBookingValue { get; set; }

        /// <summary>Gross amount ever captured from clients for this vendor's bookings (deposits included the
        /// moment they're captured), before refunds.</summary>
        public decimal TotalCollected { get; set; }

        public decimal TotalRefunded { get; set; }

        /// <summary>TotalCollected minus TotalRefunded.</summary>
        public decimal NetCollected { get; set; }

        /// <summary>What the platform has already manually paid out to this vendor.</summary>
        public decimal AmountPaidOut { get; set; }

        /// <summary>NetCollected minus AmountPaidOut, floored at zero. No commission is deducted - the
        /// platform passes collected money through to the vendor in full.</summary>
        public decimal AmountPayable { get; set; }
    }

    public class VendorFinancialFilterDto
    {
        public string? Search { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class RecordVendorPayoutDto
    {
        [System.ComponentModel.DataAnnotations.Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
        public decimal Amount { get; set; }

        public DateTimeOffset PayoutDate { get; set; } = DateTimeOffset.UtcNow;

        [System.ComponentModel.DataAnnotations.MaxLength(100)]
        public string? Reference { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(1000)]
        public string? Notes { get; set; }
    }

    public class VendorPayoutDto
    {
        public long Id { get; set; }
        public long VendorId { get; set; }
        public decimal Amount { get; set; }
        public DateTimeOffset PayoutDate { get; set; }
        public string? Reference { get; set; }
        public string? Notes { get; set; }
        public string? RecordedByAdminName { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
