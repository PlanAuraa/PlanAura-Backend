namespace Planura.Core.Application.Models;

/// <summary>
/// The server-side full-vs-deposit decision for a chosen slot + package, surfaced BEFORE the client pays so
/// booking-create can show the deposit breakdown. Computed by the same ResolvePaymentPlan the create path
/// uses, so what the client sees here is exactly what will be charged. On the full-payment path IsDeposit is
/// false, RemainderAmount is 0 and RemainderChargeDate is null — the client pays TotalAmount up front.
/// </summary>
public class PaymentPreviewDto
{
    public bool IsDeposit { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal RemainderAmount { get; set; }
    /// <summary>The date the remainder is auto-charged (event date − RemainderChargeLeadDays). Null on the full path.</summary>
    public DateOnly? RemainderChargeDate { get; set; }
    public string Currency { get; set; } = null!;
}
