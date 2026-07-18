using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models.AdminDashboard;
using Planura.Core.Application.Services.AdminDashboard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planura.Apis.Controller.Controllers
{
    [ApiController]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]

    [Route("api/admin/dashboard")]
    public class AdminDashboardController : ControllerBase
    {
        private readonly IAdminDashboardService _adminDashboardService;

        public AdminDashboardController(IAdminDashboardService adminDashboardService)
        {
            _adminDashboardService = adminDashboardService;
        }

        [HttpGet("statistics")]
        public async Task<ActionResult<DashboardStatisticsDto>> GetDashboardStatisticsAsync()
        {
            var statistics = await _adminDashboardService.GetDashboardStatisticsAsync();
            return Ok(statistics);
        }

        /// <summary>Alias of GET .../statistics matching the plan's route naming (AdminDashboardPlan.md 2.1).</summary>
        [HttpGet("summary")]
        public async Task<ActionResult<DashboardStatisticsDto>> GetDashboardSummaryAsync()
        {
            var statistics = await _adminDashboardService.GetDashboardStatisticsAsync();
            return Ok(statistics);
        }

        [HttpGet("recent-activity")]
        public async Task<IActionResult> GetRecentActivityAsync([FromQuery] int take = 20)
        {
            var activity = await _adminDashboardService.GetRecentActivityAsync(take);
            return Ok(activity);
        }
    }
}
