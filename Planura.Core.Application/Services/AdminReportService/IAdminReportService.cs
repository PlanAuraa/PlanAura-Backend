using Planura.Core.Application.Models.AdminReport;

namespace Planura.Core.Application.Services.AdminReport
{
    /// <summary>
    /// Analytics/reporting endpoints backing a practical subset of AdminDashboardPlan.md Section 3
    /// (Dashboard Overview trend charts + Reports page). See AdminReportDtos.cs for scope notes.
    /// </summary>
    public interface IAdminReportService
    {
        Task<IEnumerable<MonthlyRegistrationsDto>> GetUserRegistrationsAsync(int months = 12);
        Task<IEnumerable<MonthlyCountDto>> GetBookingsMonthlyAsync(int months = 12);
        Task<IEnumerable<MonthlyAmountDto>> GetRevenueMonthlyAsync(int months = 12);

        /// <summary>by: "revenue" (default) or "bookings".</summary>
        Task<IEnumerable<TopVendorDto>> GetTopVendorsAsync(string? by = "revenue", int take = 10);

        /// <summary>by: "vendors" (default) or "bookings".</summary>
        Task<IEnumerable<TopCategoryDto>> GetTopCategoriesAsync(string? by = "vendors", int take = 10);

        Task<VendorVerificationFunnelDto> GetVendorVerificationFunnelAsync();
    }
}
