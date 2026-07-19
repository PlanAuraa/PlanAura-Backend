using Planura.Core.Application.Models.AdminDashboard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planura.Core.Application.Services.AdminDashboard
{
    public interface IAdminDashboardService
    {
        Task<DashboardStatisticsDto> GetDashboardStatisticsAsync();

        /// <summary>Merged feed across VendorVerificationHistory and BookingStatusHistory, most recent first.</summary>
        Task<IEnumerable<RecentActivityItemDto>> GetRecentActivityAsync(int take = 20);
    }
}
