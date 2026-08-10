namespace Planura.Core.Application.Abstraction.PaymentGateway
{
    public class RefundResult
    {
        public string RefundId { get; set; } = null!;
        public string Status { get; set; } = null!;

        /// <summary>The amount actually refunded, in the gateway's smallest currency unit (e.g. cents/piastres).</summary>
        public long AmountRefundedInSmallestUnit { get; set; }
    }
}
