using Planura.Core.Application.Abstraction.Vendors.Contracts;

namespace Planura.Core.Application.Abstraction.Vendors
{
    public interface IVendorCategoryService
    {
        Task<IReadOnlyList<VendorCategoryDto>> GetActiveCategoriesAsync(CancellationToken ct = default);
    }
}
