using Planura.Core.Domain.Enums;

namespace Planura.Core.Application.Models;

public class PaymentDto
{
    public long Id { get; set; }
    public long BookingRequestId { get; set; }
    public long ClientId { get; set; }
    public long VendorId { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }
    public string? PaymentMethod { get; set; }
    public string? GatewayReference { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? RefundedAt { get; set; }
    public string? RefundReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
