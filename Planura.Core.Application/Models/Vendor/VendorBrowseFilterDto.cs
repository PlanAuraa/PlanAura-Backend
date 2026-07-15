namespace Planura.Core.Application.Models.Vendor
{
    public class VendorBrowseFilterDto
    {
        /// <summary>Category slug or numeric id.</summary>
        public string? Category { get; set; }

        public string? City { get; set; }

        public decimal? MinPrice { get; set; }

        public decimal? MaxPrice { get; set; }

        public decimal? MinRating { get; set; }

        /// <summary>"featured" (default), "rating", "priceAsc" or "priceDesc".</summary>
        public string? SortBy { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 12;
    }
}
