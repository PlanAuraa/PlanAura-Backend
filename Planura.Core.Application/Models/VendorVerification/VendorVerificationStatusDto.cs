using System;

namespace Planura.Core.Application.Models.VendorVerification
{
    public class VendorVerificationStatusDto
    {
        public long VendorVerificationId { get; set; }

        public string Status { get; set; } = null!;

        public DateTimeOffset? SubmittedAt { get; set; }
    }
}
