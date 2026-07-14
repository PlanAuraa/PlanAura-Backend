namespace Planura.Core.Application.Models;

public class InitiatePaymentResultDto
{
    public long PaymentId { get; set; }
    public string ClientSecret { get; set; } = null!;
    public string PublishableKey { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = null!;
}
