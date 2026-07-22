using Planura.Core.Application.Abstraction.AttachementService;
using Planura.Core.Application.Models;
using Planura.Core.Application.Models.Vendor;
using Planura.Core.Application.Specifications;
using Planura.Core.Application.Specifications.Vendor;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
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

    public Task<PagedResult<VendorListItemDto>> BrowseVendorsAsync(VendorBrowseFilterDto filter)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 50 ? 12 : filter.PageSize;

        var queryable = _unitOfWork.Repository<Vendor, long>().GetQueryable()
            .Where(vendor => vendor.VerificationStatus == VerificationStatus.Verified
                || vendor.VerificationStatus == VerificationStatus.Trusted)
            .Where(vendor => vendor.Packages.Any(package => package.IsActive));

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            var category = filter.Category.Trim();
            queryable = long.TryParse(category, out var categoryId)
                ? queryable.Where(vendor => vendor.CategoryId == categoryId)
                : queryable.Where(vendor => vendor.Category != null && vendor.Category.Slug == category);
        }

        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            var city = filter.City.Trim().ToLower();
            queryable = queryable.Where(vendor => vendor.City != null && vendor.City.ToLower().Contains(city));
        }

        if (filter.MinRating is not null)
        {
            queryable = queryable.Where(vendor => vendor.AvgRating >= filter.MinRating.Value);
        }

        var projected = queryable.Select(vendor => new VendorBrowseRow
        {
            Id = vendor.Id,
            BusinessName = vendor.BusinessName,
            BusinessDescription = vendor.BusinessDescription,
            LogoUrl = vendor.LogoUrl,
            CategoryName = vendor.Category != null ? vendor.Category.NameEn : null,
            City = vendor.City,
            AvgRating = vendor.AvgRating,
            TotalReviews = vendor.TotalReviews,
            VerificationStatus = vendor.VerificationStatus,
            StartingPrice = vendor.Packages.Where(p => p.IsActive).Select(p => (decimal?)p.BasePrice).Min()
        });

        if (filter.MinPrice is not null)
        {
            projected = projected.Where(row => row.StartingPrice != null && row.StartingPrice >= filter.MinPrice.Value);
        }

        if (filter.MaxPrice is not null)
        {
            projected = projected.Where(row => row.StartingPrice != null && row.StartingPrice <= filter.MaxPrice.Value);
        }

        var totalCount = projected.Count();

        projected = filter.SortBy?.Trim().ToLowerInvariant() switch
        {
            "rating" => projected.OrderByDescending(row => row.AvgRating),
            "priceasc" => projected.OrderBy(row => row.StartingPrice ?? decimal.MaxValue),
            "pricedesc" => projected.OrderByDescending(row => row.StartingPrice ?? decimal.MinValue),
            _ => projected
                .OrderByDescending(row => row.VerificationStatus == VerificationStatus.Trusted)
                .ThenByDescending(row => row.AvgRating)
        };

        var items = projected
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList()
            .Select(MapToListItem)
            .ToList();

        return Task.FromResult(new PagedResult<VendorListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<VendorDashboardStatsDto> GetDashboardStatsAsync(long vendorId)
    {
        var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(vendorId);
        if (vendor is null)
        {
            throw new NotFoundExeption(nameof(Vendor), vendorId);
        }

        var bookings = (await _unitOfWork.Repository<BookingRequest, long>()
            .GetAllWithSpecAsync(new BookingRequestsByVendorSpecification(vendorId, null, null, false, null, null))).ToList();

        var completedPayments = await _unitOfWork.Repository<Payment, long>()
            .GetAllWithSpecAsync(new PaymentsByVendorSpecification(vendorId, PaymentStatus.Completed));

        var activePackages = await _unitOfWork.Repository<VendorPackage, long>()
            .GetCountAsync(new VendorPackagesByVendorSpecification(vendorId, activeOnly: true));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return new VendorDashboardStatsDto
        {
            TotalBookingRequests = bookings.Count,
            PendingRequests = bookings.Count(b => b.Status == BookingStatus.Pending),
            AcceptedRequests = bookings.Count(b => b.Status == BookingStatus.Accepted),
            RejectedRequests = bookings.Count(b => b.Status == BookingStatus.Rejected),
            CancelledRequests = bookings.Count(b => b.Status == BookingStatus.Cancelled),
            CompletedRequests = bookings.Count(b => b.Status == BookingStatus.Completed),
            ExpiredRequests = bookings.Count(b => b.Status == BookingStatus.Expired),
            UpcomingBookings = bookings.Count(b => b.Status == BookingStatus.Accepted && b.EventDate >= today),
            TotalRevenue = completedPayments.Sum(p => p.Amount),
            AvgRating = vendor.AvgRating,
            TotalReviews = vendor.TotalReviews,
            TotalCompletedBookings = vendor.TotalCompletedBookings,
            ActivePackages = activePackages
        };
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
        CreatedAt = vendor.CreatedAt,
        PartnershipAgreementId = vendor.PartnershipAgreementId,
        PartnershipAgreementUrl = vendor.PartnershipAgreementUrl,
        PartnershipAgreementGeneratedAt = vendor.PartnershipAgreementGeneratedAt
    };

    private VendorDto ResolveImageUrls(VendorDto dto)
    {
        dto.LogoUrl = _attachmentService.ToAbsoluteUrl(dto.LogoUrl);
        dto.CoverImageUrl = _attachmentService.ToAbsoluteUrl(dto.CoverImageUrl);
        dto.PartnershipAgreementUrl = _attachmentService.ToAbsoluteUrl(dto.PartnershipAgreementUrl);
        return dto;
    }

    private VendorListItemDto MapToListItem(VendorBrowseRow row) => new()
    {
        Id = row.Id,
        BusinessName = row.BusinessName,
        LogoUrl = _attachmentService.ToAbsoluteUrl(row.LogoUrl),
        Category = row.CategoryName,
        City = row.City,
        StartingPrice = row.StartingPrice,
        AvgRating = row.AvgRating,
        ReviewCount = row.TotalReviews,
        VerificationStatus = FormatVerificationStatus(row.VerificationStatus),
        ShortDescription = row.BusinessDescription
    };

    private static string FormatVerificationStatus(string status) =>
        string.IsNullOrEmpty(status) ? status : char.ToUpperInvariant(status[0]) + status[1..];

    private sealed class VendorBrowseRow
    {
        public long Id { get; set; }
        public string BusinessName { get; set; } = null!;
        public string? BusinessDescription { get; set; }
        public string? LogoUrl { get; set; }
        public string? CategoryName { get; set; }
        public string? City { get; set; }
        public decimal AvgRating { get; set; }
        public int TotalReviews { get; set; }
        public string VerificationStatus { get; set; } = null!;
        public decimal? StartingPrice { get; set; }
    }
}
