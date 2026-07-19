using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Planura.Core.Domain.Repositories;

public interface IGenericRepository<TEntity, TKey> where TEntity : class
{
    Task<TEntity?> GetAsync(TKey id);
    Task<IEnumerable<TEntity>> GetAllAsync(bool withTracking = false);
    IQueryable<TEntity> GetAllQueryableAsync(bool withTracking = false);
    
    Task<IEnumerable<TEntity>> GetAllWithSpecAsync(ISpecification<TEntity> spec, bool withTracking = false);
    Task<TEntity?> GetWithSpecAsync(ISpecification<TEntity> spec);
    Task<int> GetCountAsync(ISpecification<TEntity> spec);
    Task<decimal> GetSumAsync( ISpecification<TEntity> spec, Expression<Func<TEntity, decimal>> selector);
    Task AddAsync(TEntity entity);
    Task AddRangeAsync(IEnumerable<TEntity> entities);
    void Update(TEntity entity);
    void Delete(TEntity entity);
    void DeleteRange(IEnumerable<TEntity> entities);
    IQueryable<TEntity> GetQueryable();
}
