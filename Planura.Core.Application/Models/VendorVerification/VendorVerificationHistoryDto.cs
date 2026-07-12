using System;

namespace Planura.Core.Application.Models.VendorVerification
{
    public class VendorVerificationHistoryDto
    {
        public long VendorVerificationId { get; set; }

        public string? PreviousStatus { get; set; }

        public string NewStatus { get; set; } = null!;

        public string? ChangedByAdminName { get; set; }

        public string? Notes { get; set; }

        public DateTimeOffset ChangedAt { get; set; }
    }
}
