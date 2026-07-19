using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models.Notification;
using Planura.Core.Application.Services;
using Planura.Core.Domain.Constants;
using Planura.Shared.Errors.Models;

namespace Planura.Apis.Controllers;

/// <summary>
/// Broadcast composer (AdminDashboardPlan.md 2.12) - thinly wraps the already-implemented
/// NotificationService.NotifyRoleAsync, which previously had no controller to call it from. The
/// admin's own inbox is served by the existing, role-agnostic NotificationsController
/// (GET/POST api/notifications) - unchanged here.
/// </summary>
[ApiController]
[Route("api/admin/notifications")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class AdminNotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public AdminNotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpPost("broadcast")]
    public async Task<IActionResult> Broadcast([FromBody] BroadcastNotificationDto dto)
    {
        var role = dto.Role.Trim().ToLowerInvariant();

        switch (role)
        {
            case Roles.Client:
                await _notificationService.NotifyRoleAsync(Roles.Client, dto.Type, dto.Title, dto.Body);
                break;
            case Roles.Vendor:
                await _notificationService.NotifyRoleAsync(Roles.Vendor, dto.Type, dto.Title, dto.Body);
                break;
            case "all":
                await _notificationService.NotifyRoleAsync(Roles.Client, dto.Type, dto.Title, dto.Body);
                await _notificationService.NotifyRoleAsync(Roles.Vendor, dto.Type, dto.Title, dto.Body);
                break;
            default:
                throw new BadRequestExeption("Role must be one of: client, vendor, all.");
        }

        return Ok(new { Message = $"Broadcast sent to {role}." });
    }
}
