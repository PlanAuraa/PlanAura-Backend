using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Planura.Core.Domain.Repositories;

namespace Planura.Infrastructure.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly PlanuraDbContext _dbContext;
    private Hashtable? _repositories;

    public UnitOfWork(PlanuraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IGenericRepository<TEntity, TKey> Repository<TEntity, TKey>() where TEntity : class
    {
        _repositories ??= new Hashtable();

        var type = typeof(TEntity).Name;

        if (!_repositories.ContainsKey(type))
        {
            var repositoryType = typeof(GenericRepository<,>);
            var repositoryInstance = Activator.CreateInstance(repositoryType.MakeGenericType(typeof(TEntity), typeof(TKey)), _dbContext);

            _repositories[type] = repositoryInstance;
        }

        return (IGenericRepository<TEntity, TKey>)_repositories[type]!;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
