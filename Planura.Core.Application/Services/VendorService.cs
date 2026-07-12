using Planura.Core.Application.Abstraction.AttachementService;
using Planura.Core.Application.Models.Vendor;
using Planura.Core.Application.Specifications.Vendor;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services;

public class VendorService : IVendorService
{
    private const string VendorLogoFolder = "vendor-logos";
    private const string VendorCoverFolder = "vendor-covers";
    private const string VendorPortfolioFolder = "vendor-portfolio";
    private const string PortfolioMediaTypeImage = "image";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAttachmentService _attachmentService;

    public VendorService(IUnitOfWork unitOfWork, IAttachmentService attachmentService)
    {
        _unitOfWork = unitOfWork;
        _attachmentService = attachmentService;
    }

    public async Task<VendorDto> GetByIdAsync(long vendorId)
    {
        var vendor = await _unitOfWork.Repository<Vendor, long>()
            .GetWithSpecAsync(new VendorProfileSpecification(vendorId));

        if (vendor is null)
        {
            throw new NotFoundExeption(nameof(Vendor), vendorId);
        }

        return ResolveImageUrls(MapToDto(vendor));
    }

    public async Task<VendorDto> UpdateProfileAsync(long vendorId, UpdateVendorProfileDto dto)
    {
        var repository = _unitOfWork.Repository<Vendor, long>();
        var vendor = await repository.GetWithSpecAsync(new VendorProfileSpecification(vendorId));

        if (vendor is null)
        {
            throw new NotFoundExeption(nameof(Vendor), vendorId);
        }

        if (string.IsNullOrWhiteSpace(dto.BusinessName))
        {
            throw new BadRequestExeption("Business name is required.");
        }

        if (dto.CategoryId is not null)
        {
            var category = await _unitOfWork.Repository<ServiceCategory, long>().GetAsync(dto.CategoryId.Value);
            if (category is null)
            {
                throw new NotFoundExeption(nameof(ServiceCategory), dto.CategoryId.Value);
            }
        }

        vendor.BusinessName = dto.BusinessName;
        vendor.BusinessDescription = dto.BusinessDescription;
        vendor.CategoryId = dto.CategoryId;
        vendor.City = dto.City;
        vendor.Address = dto.Address;
        vendor.Latitude = dto.Latitude;
        vendor.Longitude = dto.Longitude;

        if (dto.LogoFile is not null && dto.LogoFile.Length > 0)
        {
            _attachmentService.Delete(vendor.LogoUrl ?? string.Empty);
            vendor.LogoUrl = await _attachmentService.UploadAsynce(dto.LogoFile, VendorLogoFolder);
        }

        if (dto.CoverImageFile is not null && dto.CoverImageFile.Length > 0)
        {
            _attachmentService.Delete(vendor.CoverImageUrl ?? string.Empty);
            vendor.CoverImageUrl = await _attachmentService.UploadAsynce(dto.CoverImageFile, VendorCoverFolder);
        }

        vendor.UpdatedAt = DateTimeOffset.UtcNow;
        repository.Update(vendor);
        await _unitOfWork.SaveChangesAsync();

        // Re-fetch so the returned DTO reflects the (possibly changed) category join correctly.
        return await GetByIdAsync(vendorId);
    }

    public async Task<IEnumerable<PortfolioMediaItemDto>> GetPortfolioMediaAsync(long vendorId)
    {
        await EnsureVendorExistsAsync(vendorId);

        var media = await _unitOfWork.Repository<PortfolioMedia, long>()
            .GetAllWithSpecAsync(new PortfolioMediaByVendorSpecification(vendorId));

        return media.Select(m => new PortfolioMediaItemDto
        {
            Id = m.Id,
            FileUrl = _attachmentService.ToAbsoluteUrl(m.FileUrl),
            Title = m.Title,
            DisplayOrder = m.DisplayOrder
        });
    }

    public async Task<PortfolioMediaItemDto> AddPortfolioMediaAsync(long vendorId, AddPortfolioMediaDto dto)
    {
        await EnsureVendorExistsAsync(vendorId);

        if (dto.File is null || dto.File.Length == 0)
        {
            throw new BadRequestExeption("An image file is required.");
        }

        var mediaRepository = _unitOfWork.Repository<PortfolioMedia, long>();
        var existing = await mediaRepository.GetAllWithSpecAsync(new PortfolioMediaByVendorSpecification(vendorId));
        var existingList = existing.ToList();
        var nextDisplayOrder = existingList.Count == 0 ? 0 : existingList.Max(m => m.DisplayOrder) + 1;

        var fileUrl = await _attachmentService.UploadAsynce(dto.File, VendorPortfolioFolder);

        var media = new PortfolioMedia
        {
            VendorId = vendorId,
            MediaType = PortfolioMediaTypeImage,
            FileUrl = fileUrl!,
            Title = dto.Title,
            FileSizeKb = (int)(dto.File.Length / 1024),
            DisplayOrder = nextDisplayOrder
        };

        await mediaRepository.AddAsync(media);
        await _unitOfWork.SaveChangesAsync();

        return new PortfolioMediaItemDto
        {
            Id = media.Id,
            FileUrl = _attachmentService.ToAbsoluteUrl(media.FileUrl),
            Title = media.Title,
            DisplayOrder = media.DisplayOrder
        };
    }

    public async Task RemovePortfolioMediaAsync(long vendorId, long mediaId)
    {
        var repository = _unitOfWork.Repository<PortfolioMedia, long>();
        var media = await repository.GetAsync(mediaId);

        if (media is null || media.VendorId != vendorId)
        {
            throw new NotFoundExeption(nameof(PortfolioMedia), mediaId);
        }

        _attachmentService.Delete(media.FileUrl);
        repository.Delete(media);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ReorderPortfolioMediaAsync(long vendorId, ReorderPortfolioMediaDto dto)
    {
        var repository = _unitOfWork.Repository<PortfolioMedia, long>();
        var media = (await repository.GetAllWithSpecAsync(new PortfolioMediaByVendorSpecification(vendorId))).ToList();

        var providedIds = dto.OrderedMediaIds ?? new List<long>();
        var existingIds = media.Select(m => m.Id).OrderBy(id => id).ToList();

        if (providedIds.Count != existingIds.Count ||
            !providedIds.OrderBy(id => id).SequenceEqual(existingIds))
        {
            throw new BadRequestExeption("The provided media ids must exactly match the vendor's existing portfolio media.");
        }

        for (var index = 0; index < providedIds.Count; index++)
        {
            var item = media.First(m => m.Id == providedIds[index]);
            item.DisplayOrder = index;
            repository.Update(item);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<PortfolioLinkDto>> GetPortfolioLinksAsync(long vendorId)
    {
        await EnsureVendorExistsAsync(vendorId);

        var links = await _unitOfWork.Repository<PortfolioLink, long>()
            .GetAllWithSpecAsync(new PortfolioLinksByVendorSpecification(vendorId));

        return links.Select(link => new PortfolioLinkDto
        {
            Id = link.Id,
            Platform = link.Platform,
            Url = link.Url,
            Title = link.Title
        });
    }

    public async Task<PortfolioLinkDto> AddPortfolioLinkAsync(long vendorId, CreatePortfolioLinkDto dto)
    {
        await EnsureVendorExistsAsync(vendorId);

        if (string.IsNullOrWhiteSpace(dto.Platform))
        {
            throw new BadRequestExeption("Platform is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Url) || !Uri.TryCreate(dto.Url, UriKind.Absolute, out _))
        {
            throw new BadRequestExeption("A valid absolute URL is required.");
        }

        var link = new PortfolioLink
        {
            VendorId = vendorId,
            Platform = dto.Platform,
            Url = dto.Url,
            Title = dto.Title
        };

        await _unitOfWork.Repository<PortfolioLink, long>().AddAsync(link);
        await _unitOfWork.SaveChangesAsync();

        return new PortfolioLinkDto
        {
            Id = link.Id,
            Platform = link.Platform,
            Url = link.Url,
            Title = link.Title
        };
    }

    public async Task RemovePortfolioLinkAsync(long vendorId, long linkId)
    {
        var repository = _unitOfWork.Repository<PortfolioLink, long>();
        var link = await repository.GetAsync(linkId);

        if (link is null || link.VendorId != vendorId)
        {
            throw new NotFoundExeption(nameof(PortfolioLink), linkId);
        }

        repository.Delete(link);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task EnsureVendorExistsAsync(long vendorId)
    {
        var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(vendorId);
        if (vendor is null)
        {
            throw new NotFoundExeption(nameof(Vendor), vendorId);
        }
    }

    private static VendorDto MapToDto(Vendor vendor) => new()
    {
        Id = vendor.Id,
        BusinessName = vendor.BusinessName,
        BusinessDescription = vendor.BusinessDescription,
        CategoryId = vendor.CategoryId,
        CategoryName = vendor.Category?.NameEn,
        City = vendor.City,
        Address = vendor.Address,
        Latitude = vendor.Latitude,
        Longitude = vendor.Longitude,
        CoverImageUrl = vendor.CoverImageUrl,
        LogoUrl = vendor.LogoUrl,
        VerificationStatus = vendor.VerificationStatus,
        VendorType = vendor.VendorType,
        AvgRating = vendor.AvgRating,
        TotalReviews = vendor.TotalReviews,
        TotalCompletedBookings = vendor.TotalCompletedBookings,
        CreatedAt = vendor.CreatedAt
    };

    private VendorDto ResolveImageUrls(VendorDto dto)
    {
        dto.LogoUrl = _attachmentService.ToAbsoluteUrl(dto.LogoUrl);
        dto.CoverImageUrl = _attachmentService.ToAbsoluteUrl(dto.CoverImageUrl);
        return dto;
    }
}
