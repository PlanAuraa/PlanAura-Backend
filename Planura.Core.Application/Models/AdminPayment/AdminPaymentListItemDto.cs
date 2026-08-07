using Planura.Core.Domain.Enums;
using System;

namespace Planura.Core.Application.Models.AdminPayment
{
    /// <summary>Row shape for the platform-wide "Payments &amp; Transactions" admin list (AdminDashboardPlan.md 2.9).</summary>
    public class AdminPaymentListItemDto
    {
        public long PaymentId { get; set; }
        public long BookingRequestId { get; set; }
        public long ClientId { get; set; }
        public string? ClientName { get; set; }
        public string? ClientEmail { get; set; }
        public string? ClientPhone { get; set; }
        public long VendorId { get; set; }
        public string? VendorName { get; set; }
        public decimal Amount { get; set; }
        public PaymentStatus Status { get; set; }
        public string? PaymentMethod { get; set; }
        public string? GatewayReference { get; set; }
        public DateTimeOffset? PaidAt { get; set; }
        public DateTimeOffset? RefundedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class AdminPaymentFilterDto
    {
        public PaymentStatus? Status { get; set; }
        public long? VendorId { get; set; }
        public long? ClientId { get; set; }
        public DateTimeOffset? From { get; set; }
        public DateTimeOffset? To { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class AdminPaymentSummaryDto
    {
        public decimal GrossRevenue { get; set; }
        public decimal RefundedAmount { get; set; }
        public int FailedPaymentCount { get; set; }
        public int PendingAuthorizationCount { get; set; }
    }

    public class RefundPaymentDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MaxLength(500)]
        public string Reason { get; set; } = null!;

        /// <summary>Full refund when null; otherwise a partial refund amount in major currency units (e.g. EGP).</summary>
        public decimal? Amount { get; set; }
    }
}
