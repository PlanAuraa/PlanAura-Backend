namespace Planura.Core.Application.Models.AdminVendor
{
    public class AdminVendorFilterDto
    {
        /// <summary>Vendor.VerificationStatus value: unverified | pending | verified | trusted | rejected.</summary>
        public string? Status { get; set; }

        public long? CategoryId { get; set; }

        public string? City { get; set; }

        /// <summary>Matches vendor/business name (case-insensitive contains).</summary>
        public string? Search { get; set; }

        public bool? IsAccountActive { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 20;
    }
}
