using Microsoft.EntityFrameworkCore;
using Planura.Core.Application.Abstraction.Vendors;
using Planura.Core.Application.Abstraction.Vendors.Contracts;
using Planura.Infrastructure.Persistence;

namespace Planura.Core.Application.Vendors
{
    public class VendorCategoryService : IVendorCategoryService
    {
        private readonly AppDbContext _dbContext;

        public VendorCategoryService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<VendorCategoryDto>> GetActiveCategoriesAsync(CancellationToken ct = default)
        {
            return await _dbContext.VendorCategories
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new VendorCategoryDto { Id = c.Id, Name = c.Name })
                .ToListAsync(ct);
        }
    }
}
