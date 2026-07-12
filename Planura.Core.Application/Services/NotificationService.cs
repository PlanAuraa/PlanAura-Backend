using Microsoft.AspNetCore.Identity;
using Planura.Core.Application.Models.Notification;
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

    public NotificationService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        UserManager<ApplicationUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _userManager = userManager;
    }

    public async Task NotifyUserAsync(long userId, string type, string title, string? body = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            IsRead = false
        };

        await _unitOfWork.Repository<Notification, long>().AddAsync(notification);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task NotifyRoleAsync(string role, string type, string title, string? body = null)
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
