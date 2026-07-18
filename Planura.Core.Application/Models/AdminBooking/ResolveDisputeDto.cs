using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planura.Core.Application.Models.AdminBooking
{
    public class ResolveDisputeDto
    {
        [Required]
        [MaxLength(500)]
        public string ResolutionNotes { get; set; } = null!;

        /// <summary>When true, refunds the booking's Completed payment as part of resolving the
        /// dispute (chains into the same admin-refund path as api/admin/payments/{id}/refund).</summary>
        public bool RefundClient { get; set; }
    }

}
