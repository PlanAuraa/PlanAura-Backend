namespace Planura.Core.Application.Models;

/// <summary>The refund the client would receive if they requested cancellation right now, per the
/// configured cancellation policy (BookingOptions.CancellationTiers). Shown before they commit to
/// requesting — the actual request locks this estimate in for admin review.</summary>
public class CancellationQuoteDto
{
    public int DaysUntilEvent { get; set; }
    public decimal RefundPercent { get; set; }
    public decimal RefundAmount { get; set; }
}
