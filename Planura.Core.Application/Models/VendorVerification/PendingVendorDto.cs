using Planura.Core.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planura.Core.Application.Models.VendorVerification
{
    public class PendingVendorDto
    {
        public long VendorId { get; set; }

        public string VendorName { get; set; } = null!;

        public string BusinessName { get; set; } = null!;

        public VendorType VendorType { get; set; }

        public string? CategoryName { get; set; }

        public DateTimeOffset? SubmittedAt { get; set; }
        public string Status { get; set; } = null!;
    }
}
