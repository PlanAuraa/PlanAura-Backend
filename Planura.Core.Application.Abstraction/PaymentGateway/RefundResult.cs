namespace Planura.Core.Application.Abstraction.PaymentGateway
{
    public class RefundResult
    {
        public string RefundId { get; set; } = null!;
        public string Status { get; set; } = null!;
    }
}
