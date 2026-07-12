using Planura.Core.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planura.Core.Application.Models.VendorVerification
{
    public class VendorDetailsDto
    {
        public long VendorId { get; set; }

        public string VendorName { get; set; } = null!;

        public string BusinessName { get; set; } = null!;

        public string? BusinessDescription { get; set; }

        public VendorType VendorType { get; set; }

        public string? CategoryName { get; set; }

        public string? City { get; set; }

        public string? Address { get; set; }

        public string VerificationStatus { get; set; } = null!;

        public DateTimeOffset SubmittedAt { get; set; }

        public DateTimeOffset? ReviewedAt { get; set; }

        public string? RejectionReason { get; set; }

        public List<VendorDocumentDto> Documents { get; set; } = [];

        public List<PortfolioMediaDto> PortfolioMedia { get; set; } = [];
    }
}
