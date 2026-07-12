using Microsoft.AspNetCore.Identity;
using Moq;
using Planura.Core.Domain.Entities;

namespace Planura.Tests.TestHelpers;

/// <summary>
/// The well-known pattern for unit testing code that depends on ASP.NET Core Identity's
/// <see cref="UserManager{TUser}"/>: its public members are virtual, so a loose mock built
/// on top of a dummy <see cref="IUserStore{TUser}"/> can override exactly the members under test
/// without needing a real database or DI container.
/// </summary>
public static class IdentityMockFactory
{
    public static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();

        return new Mock<UserManager<ApplicationUser>>(
            store.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
    }
}
