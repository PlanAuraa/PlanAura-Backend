using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models.AdminBooking
{
    public class ApproveCancellationDto
    {
        /// <summary>Overrides the policy-computed refund estimate when set; otherwise the stored
        /// CancellationRefundAmount (locked in at request time) is used. Full/partial, same convention
        /// as RefundPaymentDto.Amount.</summary>
        public decimal? Amount { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }
    }
}
