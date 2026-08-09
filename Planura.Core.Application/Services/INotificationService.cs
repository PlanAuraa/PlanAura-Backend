using Planura.Core.Application.Models.Notification;

namespace Planura.Core.Application.Services;

public interface INotificationService
{
    Task NotifyUserAsync(long userId, string type, string title, string? body = null, string? dataJson = null);
    Task NotifyRoleAsync(string role, string type, string title, string? body = null, string? dataJson = null);

    /// <summary>
    /// Creates the in-app notification (same as <see cref="NotifyUserAsync"/>) AND additionally sends an
    /// email to the user's address. The email is best-effort: a mail failure never fails the caller and
    /// never undoes the in-app notification. Used for high-importance deposit events (Phase 3). Falls back
    /// to the notification title/body when an explicit email subject/body isn't supplied.
    /// </summary>
    Task NotifyUserWithEmailAsync(
        long userId, string type, string title, string? body = null, string? dataJson = null,
        string? emailSubject = null, string? emailBody = null);

    Task<IEnumerable<NotificationDto>> GetMyNotificationsAsync(bool unreadOnly = false);
    Task MarkAsReadAsync(long notificationId);
    Task MarkAllAsReadAsync();
}
