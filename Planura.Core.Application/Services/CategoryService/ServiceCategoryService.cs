using AutoMapper;
using Planura.Core.Application.Abstraction.AttachementService;
using Planura.Core.Application.Models;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services.CategoryService;

public class ServiceCategoryService : IServiceCategoryService
{
    private const string CategoryImagesFolder = "images/categories";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IAttachmentService _attachmentService;

    public ServiceCategoryService(IUnitOfWork unitOfWork, IMapper mapper, IAttachmentService attachmentService)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _attachmentService = attachmentService;
    }

    public async Task<IEnumerable<ServiceCategoryDto>> GetAllAsync(bool activeOnly = false)
    {
        var repo = _unitOfWork.Repository<ServiceCategory, long>();
        IEnumerable<ServiceCategory> categories = activeOnly
            ? await repo.GetAllWithSpecAsync(new ActiveServiceCategoriesSpecification())
            : await repo.GetAllAsync();

        var dtos = _mapper.Map<IEnumerable<ServiceCategoryDto>>(categories).ToList();

        var vendorCounts = _unitOfWork.Repository<Vendor, long>().GetQueryable()
            .Where(vendor => vendor.CategoryId != null)
            .GroupBy(vendor => vendor.CategoryId!.Value)
            .Select(group => new { CategoryId = group.Key, Count = group.Count() })
            .ToDictionary(row => row.CategoryId, row => row.Count);

        foreach (var dto in dtos)
        {
            dto.IconUrl = _attachmentService.ToAbsoluteUrl(dto.IconUrl);
            dto.VendorCount = vendorCounts.TryGetValue(dto.Id, out var count) ? count : 0;
        }

        return dtos;
    }

    public async Task<ServiceCategoryDto> GetByIdAsync(long id)
    {
        var category = await _unitOfWork.Repository<ServiceCategory, long>()
            .GetWithSpecAsync(new ServiceCategoryByIdSpecification(id));

        if (category is null)
        {
            throw new NotFoundExeption(nameof(ServiceCategory), id);
        }

        return await ResolveVendorCountAndIconUrlAsync(_mapper.Map<ServiceCategoryDto>(category));
    }

    public async Task<ServiceCategoryDto> GetBySlugAsync(string slug)
    {
        var category = await _unitOfWork.Repository<ServiceCategory, long>()
            .GetWithSpecAsync(new ServiceCategoryBySlugSpecification(slug));

        if (category is null)
        {
            throw new NotFoundExeption(nameof(ServiceCategory), slug);
        }

        return await ResolveVendorCountAndIconUrlAsync(_mapper.Map<ServiceCategoryDto>(category));
    }

    public async Task<ServiceCategoryDto> CreateAsync(CreateServiceCategoryDto dto)
    {
        var existing = await _unitOfWork.Repository<ServiceCategory, long>()
            .GetWithSpecAsync(new ServiceCategoryBySlugSpecification(dto.Slug));

        if (existing is not null)
        {
            throw new BadRequestExeption($"A service category with slug '{dto.Slug}' already exists.");
        }

        var category = _mapper.Map<ServiceCategory>(dto);
        if (dto.IconFile is not null && dto.IconFile.Length > 0)
        {
            category.IconUrl = await _attachmentService.UploadAsynce(dto.IconFile, CategoryImagesFolder);
        }

        await _unitOfWork.Repository<ServiceCategory, long>().AddAsync(category);
        await _unitOfWork.SaveChangesAsync();

        var created = ResolveIconUrl(_mapper.Map<ServiceCategoryDto>(category));
        created.VendorCount = 0;
        return created;
    }

    public async Task<ServiceCategoryDto> UpdateAsync(long id, UpdateServiceCategoryDto dto)
    {
        var repo = _unitOfWork.Repository<ServiceCategory, long>();
        var category = await repo.GetAsync(id);

        if (category is null)
        {
            throw new NotFoundExeption(nameof(ServiceCategory), id);
        }

        var slugOwner = await repo.GetWithSpecAsync(new ServiceCategoryBySlugSpecification(dto.Slug));
        if (slugOwner is not null && slugOwner.Id != id)
        {
            throw new BadRequestExeption($"A service category with slug '{dto.Slug}' already exists.");
        }

        if (dto.IconFile is not null && dto.IconFile.Length > 0)
        {
            _attachmentService.Delete(category.IconUrl ?? string.Empty);
            category.IconUrl = await _attachmentService.UploadAsynce(dto.IconFile, CategoryImagesFolder);
        }

        category.NameEn = dto.NameEn;
        category.Slug = dto.Slug;
        category.IsActive = dto.IsActive;

        repo.Update(category);
        await _unitOfWork.SaveChangesAsync();

        return await ResolveVendorCountAndIconUrlAsync(_mapper.Map<ServiceCategoryDto>(category));
    }

    public async Task DeleteAsync(long id)
    {
        var repo = _unitOfWork.Repository<ServiceCategory, long>();
        var category = await repo.GetAsync(id);

        if (category is null)
        {
            throw new NotFoundExeption(nameof(ServiceCategory), id);
        }

        var vendorCount = _unitOfWork.Repository<Vendor, long>().GetQueryable()
            .Count(vendor => vendor.CategoryId == id);

        if (vendorCount > 0)
        {
            throw new BadRequestExeption(
                $"Cannot delete this category: {vendorCount} vendor(s) are still attached to it. " +
                "Set it inactive instead, or reassign those vendors first.");
        }

        _attachmentService.Delete(category.IconUrl ?? string.Empty);
        repo.Delete(category);
        await _unitOfWork.SaveChangesAsync();
    }

    private ServiceCategoryDto ResolveIconUrl(ServiceCategoryDto dto)
    {
        dto.IconUrl = _attachmentService.ToAbsoluteUrl(dto.IconUrl);
        return dto;
    }

    private async Task<ServiceCategoryDto> ResolveVendorCountAndIconUrlAsync(ServiceCategoryDto dto)
    {
        dto.IconUrl = _attachmentService.ToAbsoluteUrl(dto.IconUrl);
        dto.VendorCount = await Task.FromResult(
            _unitOfWork.Repository<Vendor, long>().GetQueryable().Count(vendor => vendor.CategoryId == dto.Id));
        return dto;
    }
}
