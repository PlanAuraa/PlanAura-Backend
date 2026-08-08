using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Planura.Core.Application.Models.Emails;
using Planura.Core.Application.Services;
using Planura.Core.Application.Services.Emails;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;
using Planura.Tests.TestHelpers;
using Xunit;

namespace Planura.Tests.Services;

public class NotificationServiceTests
{
    private const long UserId = 500;

    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock = IdentityMockFactory.CreateUserManagerMock();
    private readonly Mock<IEmailService> _emailServiceMock = new();

    private NotificationService CreateService() => new(
        _unitOfWorkMock.Object,
        _currentUserServiceMock.Object,
        _userManagerMock.Object,
        _emailServiceMock.Object,
        NullLogger<NotificationService>.Instance);

    private Mock<IGenericRepository<Notification, long>> SetupNotificationRepo(out List<Notification> captured)
    {
        var repo = _unitOfWorkMock.SetupRepository<Notification, long>();
        var list = new List<Notification>();
        repo.Setup(r => r.AddAsync(It.IsAny<Notification>()))
            .Callback<Notification>(n => list.Add(n))
            .Returns(Task.CompletedTask);
        captured = list;
        return repo;
    }

    [Fact]
    public async Task NotifyUserWithEmailAsync_CreatesInAppNotificationAndSendsEmail()
    {
        SetupNotificationRepo(out var captured);
        _userManagerMock.Setup(m => m.FindByIdAsync(UserId.ToString()))
            .ReturnsAsync(new ApplicationUser { Id = UserId, Email = "client@test.local" });

        Email? sentEmail = null;
        _emailServiceMock.Setup(e => e.SendEmail(It.IsAny<Email>()))
            .Callback<Email>(e => sentEmail = e)
            .Returns(Task.CompletedTask);

        await CreateService().NotifyUserWithEmailAsync(
            UserId, "remainder_failed", "Remainder charge failed", "Your remainder could not be charged.",
            emailSubject: "Action needed: remainder payment", emailBody: "Please pay your remainder.");

        var notification = Assert.Single(captured);
        Assert.Equal("remainder_failed", notification.Type);
        Assert.Equal("Remainder charge failed", notification.Title);

        Assert.NotNull(sentEmail);
        Assert.Equal("client@test.local", sentEmail!.To);
        Assert.Equal("Action needed: remainder payment", sentEmail.Subject);
        Assert.Equal("Please pay your remainder.", sentEmail.Body);
    }

    [Fact]
    public async Task NotifyUserWithEmailAsync_EmailFails_StillCreatesInAppNotificationAndDoesNotThrow()
    {
        SetupNotificationRepo(out var captured);
        _userManagerMock.Setup(m => m.FindByIdAsync(UserId.ToString()))
            .ReturnsAsync(new ApplicationUser { Id = UserId, Email = "client@test.local" });
        _emailServiceMock.Setup(e => e.SendEmail(It.IsAny<Email>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        var exception = await Record.ExceptionAsync(() => CreateService().NotifyUserWithEmailAsync(
            UserId, "remainder_paid", "Remainder paid", "Your booking is fully paid."));

        Assert.Null(exception);                 // best-effort: mail failure never fails the caller
        Assert.Single(captured);                // in-app notification still recorded
    }

    [Fact]
    public async Task NotifyUserWithEmailAsync_NoEmailAddress_SkipsEmailButStillNotifies()
    {
        SetupNotificationRepo(out var captured);
        _userManagerMock.Setup(m => m.FindByIdAsync(UserId.ToString()))
            .ReturnsAsync(new ApplicationUser { Id = UserId, Email = null });

        await CreateService().NotifyUserWithEmailAsync(UserId, "remainder_paid", "Remainder paid");

        Assert.Single(captured);
        _emailServiceMock.Verify(e => e.SendEmail(It.IsAny<Email>()), Times.Never);
    }
}
