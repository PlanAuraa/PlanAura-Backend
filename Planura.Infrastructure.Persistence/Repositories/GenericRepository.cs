using Microsoft.EntityFrameworkCore;
using Planura.Core.Domain.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Planura.Infrastructure.Persistence.Repositories;

public class GenericRepository<TEntity, TKey> : IGenericRepository<TEntity, TKey> where TEntity : class
{
    protected readonly PlanuraDbContext _dbContext;

    public GenericRepository(PlanuraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TEntity?> GetAsync(TKey id)
    {
        return await _dbContext.Set<TEntity>().FindAsync(id);
    }

    public async Task<IEnumerable<TEntity>> GetAllAsync(bool withTracking = false)
    {
        return withTracking 
            ? await _dbContext.Set<TEntity>().ToListAsync() 
            : await _dbContext.Set<TEntity>().AsNoTracking().ToListAsync();
    }

    public IQueryable<TEntity> GetAllQueryableAsync(bool withTracking = false)
    {
        return withTracking 
            ? _dbContext.Set<TEntity>() 
            : _dbContext.Set<TEntity>().AsNoTracking();
    }

    public async Task<IEnumerable<TEntity>> GetAllWithSpecAsync(ISpecification<TEntity> spec, bool withTracking = false)
    {
        var query = ApplySpecifications(spec);
        return withTracking 
            ? await query.ToListAsync() 
            : await query.AsNoTracking().ToListAsync();
    }

    public async Task<TEntity?> GetWithSpecAsync(ISpecification<TEntity> spec)
    {
        return await ApplySpecifications(spec).FirstOrDefaultAsync();
    }

    public async Task<int> GetCountAsync(ISpecification<TEntity> spec)
    {
        return await ApplySpecifications(spec).CountAsync();
    }

    public async Task AddAsync(TEntity entity)
    {
        await _dbContext.Set<TEntity>().AddAsync(entity);
    }

    public async Task AddRangeAsync(IEnumerable<TEntity> entities)
    {
        await _dbContext.Set<TEntity>().AddRangeAsync(entities);
    }

    public void Update(TEntity entity)
    {
        _dbContext.Set<TEntity>().Update(entity);
    }

    public void Delete(TEntity entity)
    {
        _dbContext.Set<TEntity>().Remove(entity);
    }

    public void DeleteRange(IEnumerable<TEntity> entities)
    {
        _dbContext.Set<TEntity>().RemoveRange(entities);
    }

    public IQueryable<TEntity> GetQueryable()
    {
        return _dbContext.Set<TEntity>();
    }
    public async Task<decimal> GetSumAsync(
    ISpecification<TEntity> spec,
    Expression<Func<TEntity, decimal>> selector)
    {
        var query = ApplySpecifications(spec);

        return await query.SumAsync(selector);
    }

    private IQueryable<TEntity> ApplySpecifications(ISpecification<TEntity> spec)
    {
        return SpecificationEvaluator<TEntity>.GetQuery(_dbContext.Set<TEntity>(), spec);
    }

    
}
