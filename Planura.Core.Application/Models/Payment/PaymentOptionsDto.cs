namespace Planura.Core.Application.Models;

public class PaymentOptionsDto
{
    public long BookingRequestId { get; set; }
    public decimal AmountDue { get; set; }
    public string Currency { get; set; } = null!;
    public bool IsPayable { get; set; }
    public string PublishableKey { get; set; } = null!;
}
