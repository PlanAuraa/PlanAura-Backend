using AutoMapper;
using Planura.Core.Application.Abstraction.AttachementService;
using Planura.Core.Application.Models;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services;

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

        var dtos = _mapper.Map<IEnumerable<ServiceCategoryDto>>(categories);
        foreach (var dto in dtos)
        {
            dto.IconUrl = _attachmentService.ToAbsoluteUrl(dto.IconUrl);
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

        return ResolveIconUrl(_mapper.Map<ServiceCategoryDto>(category));
    }

    public async Task<ServiceCategoryDto> GetBySlugAsync(string slug)
    {
        var category = await _unitOfWork.Repository<ServiceCategory, long>()
            .GetWithSpecAsync(new ServiceCategoryBySlugSpecification(slug));

        if (category is null)
        {
            throw new NotFoundExeption(nameof(ServiceCategory), slug);
        }

        return ResolveIconUrl(_mapper.Map<ServiceCategoryDto>(category));
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

        return ResolveIconUrl(_mapper.Map<ServiceCategoryDto>(category));
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

        return ResolveIconUrl(_mapper.Map<ServiceCategoryDto>(category));
    }

    public async Task DeleteAsync(long id)
    {
        var repo = _unitOfWork.Repository<ServiceCategory, long>();
        var category = await repo.GetAsync(id);

        if (category is null)
        {
            throw new NotFoundExeption(nameof(ServiceCategory), id);
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
}
