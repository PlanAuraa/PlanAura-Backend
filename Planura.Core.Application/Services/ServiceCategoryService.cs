using AutoMapper;
using Planura.Core.Application.Models;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services;

public class ServiceCategoryService : IServiceCategoryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ServiceCategoryService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IEnumerable<ServiceCategoryDto>> GetAllAsync(bool activeOnly = false)
    {
        var repo = _unitOfWork.Repository<ServiceCategory, long>();
        IEnumerable<ServiceCategory> categories = activeOnly
            ? await repo.GetAllWithSpecAsync(new ActiveServiceCategoriesSpecification())
            : await repo.GetAllAsync();

        return _mapper.Map<IEnumerable<ServiceCategoryDto>>(categories);
    }

    public async Task<ServiceCategoryDto> GetByIdAsync(long id)
    {
        var category = await _unitOfWork.Repository<ServiceCategory, long>()
            .GetWithSpecAsync(new ServiceCategoryByIdSpecification(id));

        if (category is null)
        {
            throw new NotFoundExeption(nameof(ServiceCategory), id);
        }

        return _mapper.Map<ServiceCategoryDto>(category);
    }

    public async Task<ServiceCategoryDto> GetBySlugAsync(string slug)
    {
        var category = await _unitOfWork.Repository<ServiceCategory, long>()
            .GetWithSpecAsync(new ServiceCategoryBySlugSpecification(slug));

        if (category is null)
        {
            throw new NotFoundExeption(nameof(ServiceCategory), slug);
        }

        return _mapper.Map<ServiceCategoryDto>(category);
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
        await _unitOfWork.Repository<ServiceCategory, long>().AddAsync(category);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<ServiceCategoryDto>(category);
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

        _mapper.Map(dto, category);
        repo.Update(category);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<ServiceCategoryDto>(category);
    }

    public async Task DeleteAsync(long id)
    {
        var repo = _unitOfWork.Repository<ServiceCategory, long>();
        var category = await repo.GetAsync(id);

        if (category is null)
        {
            throw new NotFoundExeption(nameof(ServiceCategory), id);
        }

        repo.Delete(category);
        await _unitOfWork.SaveChangesAsync();
    }
}
