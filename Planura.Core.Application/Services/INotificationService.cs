using Planura.Core.Application.Models.Notification;

namespace Planura.Core.Application.Services;

public interface INotificationService
{
    Task NotifyUserAsync(long userId, string type, string title, string? body = null, string? dataJson = null);
    Task NotifyRoleAsync(string role, string type, string title, string? body = null, string? dataJson = null);

    Task<IEnumerable<NotificationDto>> GetMyNotificationsAsync(bool unreadOnly = false);
    Task MarkAsReadAsync(long notificationId);
    Task MarkAllAsReadAsync();
}
