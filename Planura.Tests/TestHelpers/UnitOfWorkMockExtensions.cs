using Moq;
using Planura.Core.Domain.Repositories;

namespace Planura.Tests.TestHelpers;

/// <summary>
/// Reduces per-test boilerplate for wiring <c>_unitOfWork.Repository&lt;TEntity, TKey&gt;()</c>
/// to a fresh, independently-configurable repository mock.
/// </summary>
public static class UnitOfWorkMockExtensions
{
    public static Mock<IGenericRepository<TEntity, TKey>> SetupRepository<TEntity, TKey>(
        this Mock<IUnitOfWork> unitOfWorkMock)
        where TEntity : class
    {
        var repositoryMock = new Mock<IGenericRepository<TEntity, TKey>>();
        unitOfWorkMock
            .Setup(u => u.Repository<TEntity, TKey>())
            .Returns(repositoryMock.Object);

        return repositoryMock;
    }
}
