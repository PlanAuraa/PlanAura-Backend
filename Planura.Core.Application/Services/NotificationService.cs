using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Planura.Core.Application.Models.Emails;
using Planura.Core.Application.Models.Notification;
using Planura.Core.Application.Services.Emails;
using Planura.Core.Application.Specifications.Notification;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        ILogger<NotificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task NotifyUserAsync(long userId, string type, string title, string? body = null, string? dataJson = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            DataJson = dataJson,
            IsRead = false
        };

        await _unitOfWork.Repository<Notification, long>().AddAsync(notification);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task NotifyUserWithEmailAsync(
        long userId, string type, string title, string? body = null, string? dataJson = null,
        string? emailSubject = null, string? emailBody = null)
    {
        // The in-app notification is the durable record and must succeed; the email is a best-effort extra.
        await NotifyUserAsync(userId, type, title, body, dataJson);

        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is not null && !string.IsNullOrWhiteSpace(user.Email))
            {
                await _emailService.SendEmail(new Email
                {
                    To = user.Email,
                    Subject = emailSubject ?? title,
                    Body = emailBody ?? body ?? title
                });
            }
        }
        catch (Exception ex)
        {
            // Never let a mail failure fail the caller or undo the in-app notification.
            _logger.LogWarning(ex,
                "Failed to send email for notification type {Type} to user {UserId}; the in-app notification was still created.",
                type, userId);
        }
    }

    public async Task NotifyRoleAsync(string role, string type, string title, string? body = null, string? dataJson = null)
    {
        var users = await _userManager.GetUsersInRoleAsync(role);

        if (users.Count == 0)
        {
            return;
        }

        var notifications = users.Select(user => new Notification
        {
            UserId = user.Id,
            Type = type,
            Title = title,
            Body = body,
            DataJson = dataJson,
            IsRead = false
        });

        await _unitOfWork.Repository<Notification, long>().AddRangeAsync(notifications);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<NotificationDto>> GetMyNotificationsAsync(bool unreadOnly = false)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnAuthorizedExeption("No authenticated user.");

        var notifications = await _unitOfWork.Repository<Notification, long>()
            .GetAllWithSpecAsync(new NotificationsByUserSpecification(userId, unreadOnly));

        return notifications.Select(n => new NotificationDto
        {
            Id = n.Id,
            Type = n.Type,
            Title = n.Title,
            Body = n.Body,
            DataJson = n.DataJson,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt
        });
    }

    public async Task MarkAsReadAsync(long notificationId)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnAuthorizedExeption("No authenticated user.");

        var repository = _unitOfWork.Repository<Notification, long>();
        var notification = await repository.GetAsync(notificationId);

        if (notification is null || notification.UserId != userId)
        {
            throw new NotFoundExeption(nameof(Notification), notificationId);
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            repository.Update(notification);
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync()
    {
        var userId = _currentUserService.UserId
            ?? throw new UnAuthorizedExeption("No authenticated user.");

        var repository = _unitOfWork.Repository<Notification, long>();
        var unread = await repository.GetAllWithSpecAsync(new NotificationsByUserSpecification(userId, unreadOnly: true));

        foreach (var notification in unread)
        {
            notification.IsRead = true;
            repository.Update(notification);
        }

        await _unitOfWork.SaveChangesAsync();
    }
}
