using AutoMapper;
using Planura.Core.Application.Models;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services;

public class VendorPackageService : IVendorPackageService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public VendorPackageService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IEnumerable<VendorPackageDto>> GetAllAsync(bool activeOnly = false)
    {
        var repo = _unitOfWork.Repository<VendorPackage, long>();
        IEnumerable<VendorPackage> packages = activeOnly
            ? await repo.GetAllWithSpecAsync(new ActiveVendorPackagesSpecification())
            : await repo.GetAllAsync();

        return _mapper.Map<IEnumerable<VendorPackageDto>>(packages);
    }

    public async Task<IEnumerable<VendorPackageDto>> GetByVendorAsync(long vendorId, bool activeOnly = false)
    {
        await EnsureVendorExistsAsync(vendorId);

        var packages = await _unitOfWork.Repository<VendorPackage, long>()
            .GetAllWithSpecAsync(new VendorPackagesByVendorSpecification(vendorId, activeOnly));

        return _mapper.Map<IEnumerable<VendorPackageDto>>(packages);
    }

    public async Task<VendorPackageDto> GetByIdAsync(long id)
    {
        var package = await _unitOfWork.Repository<VendorPackage, long>()
            .GetWithSpecAsync(new VendorPackageByIdSpecification(id));

        if (package is null)
        {
            throw new NotFoundExeption(nameof(VendorPackage), id);
        }

        return _mapper.Map<VendorPackageDto>(package);
    }

    public async Task<VendorPackageDto> CreateAsync(CreateVendorPackageDto dto)
    {
        await EnsureVendorExistsAsync(dto.VendorId);
        ValidatePackage(dto.BasePrice, dto.Currency, dto.Title);

        var package = _mapper.Map<VendorPackage>(dto);
        await _unitOfWork.Repository<VendorPackage, long>().AddAsync(package);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<VendorPackageDto>(package);
    }

    public async Task<VendorPackageDto> UpdateAsync(long id, UpdateVendorPackageDto dto)
    {
        var repo = _unitOfWork.Repository<VendorPackage, long>();
        var package = await repo.GetWithSpecAsync(new VendorPackageByIdSpecification(id));

        if (package is null)
        {
            throw new NotFoundExeption(nameof(VendorPackage), id);
        }

        ValidatePackage(dto.BasePrice, dto.Currency, dto.Title);

        _mapper.Map(dto, package);
        package.UpdatedAt = DateTimeOffset.UtcNow;
        repo.Update(package);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<VendorPackageDto>(package);
    }

    public async Task DeleteAsync(long id)
    {
        var repo = _unitOfWork.Repository<VendorPackage, long>();
        var package = await repo.GetAsync(id);

        if (package is null)
        {
            throw new NotFoundExeption(nameof(VendorPackage), id);
        }

        repo.Delete(package);
        await _unitOfWork.SaveChangesAsync();
    }

    public Task<IEnumerable<VendorPackageDto>> SearchAsync(VendorPackageSearchDto query)
    {
        var queryable = _unitOfWork.Repository<VendorPackage, long>().GetQueryable();

        if (query.ActiveOnly)
        {
            queryable = queryable.Where(package => package.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Title))
        {
            var title = query.Title.Trim().ToLower();
            queryable = queryable.Where(package => package.Title.ToLower().Contains(title));
        }

        if (query.CategoryId is not null && query.CategoryId > 0)
        {
            var categoryId = query.CategoryId.Value;
            queryable = queryable.Where(package => package.Vendor.CategoryId == categoryId);
        }

        var packages = queryable
            .OrderByDescending(package => package.CreatedAt)
            .ToList();

        var result = _mapper.Map<IEnumerable<VendorPackageDto>>(packages);
        return Task.FromResult(result);
    }

    private async Task EnsureVendorExistsAsync(long vendorId)
    {
        var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(vendorId);
        if (vendor is null)
        {
            throw new NotFoundExeption(nameof(Vendor), vendorId);
        }
    }

    private static void ValidatePackage(decimal basePrice, string currency, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new BadRequestExeption("Title is required.");
        }

        if (basePrice < 0)
        {
            throw new BadRequestExeption("Base price cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Length > 3)
        {
            throw new BadRequestExeption("Currency must be a 3-letter ISO code (e.g. EGP, USD).");
        }
    }
}
