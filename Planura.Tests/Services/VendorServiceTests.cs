using Microsoft.AspNetCore.Http;
using Moq;
using Planura.Core.Application.Abstraction.AttachementService;
using Planura.Core.Application.Models.Vendor;
using Planura.Core.Application.Services;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;
using Planura.Tests.TestHelpers;
using Xunit;

namespace Planura.Tests.Services;

public class VendorServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IAttachmentService> _attachmentServiceMock = new();

    public VendorServiceTests()
    {
        _attachmentServiceMock
            .Setup(a => a.ToAbsoluteUrl(It.IsAny<string?>()))
            .Returns((string? path) => path);
    }

    private VendorService CreateService() => new(_unitOfWorkMock.Object, _attachmentServiceMock.Object);

    private static Vendor CreateVendor(long id = 1) => new()
    {
        Id = id,
        UserId = 100 + id,
        BusinessName = "Test Business"
    };

    private void SetupVendorQueryable(IEnumerable<Vendor> vendors)
    {
        var vendorRepo = _unitOfWorkMock.SetupRepository<Vendor, long>();
        vendorRepo.Setup(r => r.GetQueryable()).Returns(vendors.AsQueryable());
    }

    private static Vendor CreateBrowsableVendor(
        long id,
        string status = "verified",
        decimal avgRating = 4.0m,
        string city = "Cairo",
        long? categoryId = null,
        ServiceCategory? category = null,
        params decimal[] activePackagePrices)
    {
        var vendor = new Vendor
        {
            Id = id,
            UserId = 100 + id,
            BusinessName = $"Vendor {id}",
            VerificationStatus = status,
            AvgRating = avgRating,
            TotalReviews = 10,
            City = city,
            CategoryId = categoryId,
            Category = category
        };

        vendor.Packages = activePackagePrices
            .Select((price, index) => new VendorPackage
            {
                Id = id * 100 + index,
                VendorId = id,
                Title = $"Package {index}",
                BasePrice = price,
                IsActive = true,
                Vendor = vendor
            })
            .ToList();

        return vendor;
    }

    [Fact]
    public async Task ReorderPortfolioMediaAsync_IdSetMismatch_ThrowsBadRequest()
    {
        var existingMedia = new List<PortfolioMedia>
        {
            new() { Id = 1, VendorId = 1, DisplayOrder = 0, FileUrl = "a.jpg", MediaType = "image" },
            new() { Id = 2, VendorId = 1, DisplayOrder = 1, FileUrl = "b.jpg", MediaType = "image" }
        };

        var mediaRepo = _unitOfWorkMock.SetupRepository<PortfolioMedia, long>();
        mediaRepo
            .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<PortfolioMedia>>(), It.IsAny<bool>()))
            .ReturnsAsync(existingMedia);

        var dto = new ReorderPortfolioMediaDto { OrderedMediaIds = new List<long> { 1, 3 } };

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.ReorderPortfolioMediaAsync(1, dto));
    }

    [Fact]
    public async Task ReorderPortfolioMediaAsync_ValidIds_UpdatesDisplayOrderToMatchNewSequence()
    {
        var media1 = new PortfolioMedia { Id = 1, VendorId = 1, DisplayOrder = 0, FileUrl = "a.jpg", MediaType = "image" };
        var media2 = new PortfolioMedia { Id = 2, VendorId = 1, DisplayOrder = 1, FileUrl = "b.jpg", MediaType = "image" };
        var media3 = new PortfolioMedia { Id = 3, VendorId = 1, DisplayOrder = 2, FileUrl = "c.jpg", MediaType = "image" };
        var existingMedia = new List<PortfolioMedia> { media1, media2, media3 };

        var mediaRepo = _unitOfWorkMock.SetupRepository<PortfolioMedia, long>();
        mediaRepo
            .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<PortfolioMedia>>(), It.IsAny<bool>()))
            .ReturnsAsync(existingMedia);

        var dto = new ReorderPortfolioMediaDto { OrderedMediaIds = new List<long> { 3, 1, 2 } };

        var service = CreateService();
        await service.ReorderPortfolioMediaAsync(1, dto);

        Assert.Equal(0, media3.DisplayOrder);
        Assert.Equal(1, media1.DisplayOrder);
        Assert.Equal(2, media2.DisplayOrder);

        mediaRepo.Verify(r => r.Update(It.IsAny<PortfolioMedia>()), Times.Exactly(3));
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemovePortfolioMediaAsync_MediaBelongsToDifferentVendor_ThrowsNotFound()
    {
        var media = new PortfolioMedia { Id = 5, VendorId = 999, DisplayOrder = 0, FileUrl = "a.jpg", MediaType = "image" };

        var mediaRepo = _unitOfWorkMock.SetupRepository<PortfolioMedia, long>();
        mediaRepo.Setup(r => r.GetAsync(5)).ReturnsAsync(media);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.RemovePortfolioMediaAsync(1, 5));

        mediaRepo.Verify(r => r.Delete(It.IsAny<PortfolioMedia>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfileAsync_CategoryNotFound_ThrowsNotFound()
    {
        var vendor = CreateVendor();

        var vendorRepo = _unitOfWorkMock.SetupRepository<Vendor, long>();
        vendorRepo
            .Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Vendor>>()))
            .ReturnsAsync(vendor);

        var categoryRepo = _unitOfWorkMock.SetupRepository<ServiceCategory, long>();
        categoryRepo.Setup(r => r.GetAsync(42)).ReturnsAsync((ServiceCategory?)null);

        var dto = new UpdateVendorProfileDto
        {
            BusinessName = "Updated Name",
            CategoryId = 42
        };

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.UpdateProfileAsync(vendor.Id, dto));
    }

    [Fact]
    public async Task AddPortfolioMediaAsync_ComputesNextDisplayOrderFromExistingMax()
    {
        var vendor = CreateVendor();

        var vendorRepo = _unitOfWorkMock.SetupRepository<Vendor, long>();
        vendorRepo.Setup(r => r.GetAsync(vendor.Id)).ReturnsAsync(vendor);

        var existingMedia = new List<PortfolioMedia>
        {
            new() { Id = 1, VendorId = vendor.Id, DisplayOrder = 0, FileUrl = "a.jpg", MediaType = "image" },
            new() { Id = 2, VendorId = vendor.Id, DisplayOrder = 3, FileUrl = "b.jpg", MediaType = "image" }
        };

        var mediaRepo = _unitOfWorkMock.SetupRepository<PortfolioMedia, long>();
        mediaRepo
            .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<PortfolioMedia>>(), It.IsAny<bool>()))
            .ReturnsAsync(existingMedia);

        PortfolioMedia? captured = null;
        mediaRepo
            .Setup(r => r.AddAsync(It.IsAny<PortfolioMedia>()))
            .Callback<PortfolioMedia>(m => captured = m)
            .Returns(Task.CompletedTask);

        _attachmentServiceMock
            .Setup(a => a.UploadAsynce(It.IsAny<IFormFile>(), It.IsAny<string>()))
            .ReturnsAsync("images/vendor-portfolio/new.jpg");

        var dto = new AddPortfolioMediaDto { File = FormFileFactory.Create(), Title = "New shot" };

        var service = CreateService();
        await service.AddPortfolioMediaAsync(vendor.Id, dto);

        Assert.NotNull(captured);
        Assert.Equal(4, captured!.DisplayOrder);
    }

    [Fact]
    public async Task GetDashboardStatsAsync_TotalRevenue_UsesFullCapturedTotalAndIncludesFullyPaidDeposits()
    {
        var vendor = CreateVendor(1);

        var vendorRepo = _unitOfWorkMock.SetupRepository<Vendor, long>();
        vendorRepo.Setup(r => r.GetAsync(vendor.Id)).ReturnsAsync(vendor);

        var bookingRepo = _unitOfWorkMock.SetupRepository<BookingRequest, long>();
        bookingRepo
            .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<BookingRequest>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<BookingRequest>());

        // The spec is applied by the (mocked) repo; this list stands in for the revenue-counted payments it
        // returns. A deposit booking's Amount is only the deposit (200) while TotalAmount is the full captured
        // price (1000) — the sum must use TotalAmount. The legacy row (null TotalAmount) falls back to Amount.
        var revenuePayments = new List<Payment>
        {
            new() { Id = 1, VendorId = 1, IsDeposit = true, Amount = 200m, TotalAmount = 1000m, Status = PaymentStatus.FullyPaid },
            new() { Id = 2, VendorId = 1, IsDeposit = false, Amount = 500m, TotalAmount = 500m, Status = PaymentStatus.Completed },
            new() { Id = 3, VendorId = 1, IsDeposit = false, Amount = 300m, TotalAmount = null, Status = PaymentStatus.Completed }
        };

        var paymentRepo = _unitOfWorkMock.SetupRepository<Payment, long>();
        paymentRepo
            .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Payment>>(), It.IsAny<bool>()))
            .ReturnsAsync(revenuePayments);

        var packageRepo = _unitOfWorkMock.SetupRepository<VendorPackage, long>();
        packageRepo
            .Setup(r => r.GetCountAsync(It.IsAny<ISpecification<VendorPackage>>()))
            .ReturnsAsync(0);

        var result = await CreateService().GetDashboardStatsAsync(vendor.Id);

        // 1000 (deposit total, not the 200 deposit) + 500 + 300 (legacy fallback). Old code summed Amount => 1000.
        Assert.Equal(1800m, result.TotalRevenue);
    }

    [Fact]
    public async Task BrowseVendorsAsync_ExcludesUnverifiedPendingAndRejectedVendors()
    {
        var vendors = new List<Vendor>
        {
            CreateBrowsableVendor(1, status: VerificationStatus.Unverified),
            CreateBrowsableVendor(2, status: VerificationStatus.Pending),
            CreateBrowsableVendor(3, status: VerificationStatus.Rejected),
            CreateBrowsableVendor(4, status: VerificationStatus.Verified),
            CreateBrowsableVendor(5, status: VerificationStatus.Trusted)
        };
        SetupVendorQueryable(vendors);

        var result = await CreateService().BrowseVendorsAsync(new VendorBrowseFilterDto());

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.Contains(item.Id, new[] { 4L, 5L }));
    }

    [Fact]
    public async Task BrowseVendorsAsync_FiltersByCategoryId()
    {
        var catering = new ServiceCategory { Id = 1, NameEn = "Catering", Slug = "catering" };
        var venues = new ServiceCategory { Id = 2, NameEn = "Venues", Slug = "venues" };

        var vendors = new List<Vendor>
        {
            CreateBrowsableVendor(1, categoryId: 1, category: catering),
            CreateBrowsableVendor(2, categoryId: 2, category: venues)
        };
        SetupVendorQueryable(vendors);

        var result = await CreateService().BrowseVendorsAsync(new VendorBrowseFilterDto { Category = "1" });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.Items.Single().Id);
        Assert.Equal("Catering", result.Items.Single().Category);
    }

    [Fact]
    public async Task BrowseVendorsAsync_FiltersByCategorySlug()
    {
        var catering = new ServiceCategory { Id = 1, NameEn = "Catering", Slug = "catering" };
        var venues = new ServiceCategory { Id = 2, NameEn = "Venues", Slug = "venues" };

        var vendors = new List<Vendor>
        {
            CreateBrowsableVendor(1, categoryId: 1, category: catering),
            CreateBrowsableVendor(2, categoryId: 2, category: venues)
        };
        SetupVendorQueryable(vendors);

        var result = await CreateService().BrowseVendorsAsync(new VendorBrowseFilterDto { Category = "venues" });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(2, result.Items.Single().Id);
    }

    [Fact]
    public async Task BrowseVendorsAsync_FiltersByCity_CaseInsensitiveContains()
    {
        var vendors = new List<Vendor>
        {
            CreateBrowsableVendor(1, city: "Cairo"),
            CreateBrowsableVendor(2, city: "New Cairo"),
            CreateBrowsableVendor(3, city: "Alexandria")
        };
        SetupVendorQueryable(vendors);

        var result = await CreateService().BrowseVendorsAsync(new VendorBrowseFilterDto { City = "cairo" });

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.Contains(item.Id, new[] { 1L, 2L }));
    }

    [Fact]
    public async Task BrowseVendorsAsync_FiltersByPriceRange_UsesMinActivePackagePrice()
    {
        var vendors = new List<Vendor>
        {
            CreateBrowsableVendor(1, activePackagePrices: new decimal[] { 500m, 300m }),
            CreateBrowsableVendor(2, activePackagePrices: new decimal[] { 1500m }),
            CreateBrowsableVendor(3, activePackagePrices: new decimal[] { 5000m })
        };
        SetupVendorQueryable(vendors);

        var result = await CreateService().BrowseVendorsAsync(new VendorBrowseFilterDto { MinPrice = 400m, MaxPrice = 2000m });

        Assert.Equal(1, result.TotalCount);
        var item = result.Items.Single();
        Assert.Equal(2, item.Id);
        Assert.Equal(1500m, item.StartingPrice);
    }

    [Fact]
    public async Task BrowseVendorsAsync_StartingPrice_NullWhenNoActivePackages()
    {
        var vendorWithInactiveOnly = CreateBrowsableVendor(1);
        vendorWithInactiveOnly.Packages = new List<VendorPackage>
        {
            new() { Id = 10, VendorId = 1, Title = "Inactive", BasePrice = 100m, IsActive = false, Vendor = vendorWithInactiveOnly }
        };

        SetupVendorQueryable(new[] { vendorWithInactiveOnly });

        var result = await CreateService().BrowseVendorsAsync(new VendorBrowseFilterDto());

        Assert.Null(result.Items.Single().StartingPrice);
    }

    [Fact]
    public async Task BrowseVendorsAsync_FiltersByMinRating()
    {
        var vendors = new List<Vendor>
        {
            CreateBrowsableVendor(1, avgRating: 3.5m),
            CreateBrowsableVendor(2, avgRating: 4.8m)
        };
        SetupVendorQueryable(vendors);

        var result = await CreateService().BrowseVendorsAsync(new VendorBrowseFilterDto { MinRating = 4.5m });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(2, result.Items.Single().Id);
    }

    [Fact]
    public async Task BrowseVendorsAsync_CombinedFilters_AppliesAllConditions()
    {
        var catering = new ServiceCategory { Id = 1, NameEn = "Catering", Slug = "catering" };

        var vendors = new List<Vendor>
        {
            // matches everything
            CreateBrowsableVendor(1, status: VerificationStatus.Trusted, avgRating: 4.7m, city: "Cairo", categoryId: 1, category: catering, activePackagePrices: new decimal[] { 800m }),
            // wrong category
            CreateBrowsableVendor(2, status: VerificationStatus.Verified, avgRating: 4.9m, city: "Cairo", categoryId: 2, category: new ServiceCategory { Id = 2, NameEn = "Venues", Slug = "venues" }, activePackagePrices: new decimal[] { 800m }),
            // wrong city
            CreateBrowsableVendor(3, status: VerificationStatus.Verified, avgRating: 4.9m, city: "Alexandria", categoryId: 1, category: catering, activePackagePrices: new decimal[] { 800m }),
            // rating too low
            CreateBrowsableVendor(4, status: VerificationStatus.Verified, avgRating: 2.0m, city: "Cairo", categoryId: 1, category: catering, activePackagePrices: new decimal[] { 800m }),
            // price out of range
            CreateBrowsableVendor(5, status: VerificationStatus.Verified, avgRating: 4.9m, city: "Cairo", categoryId: 1, category: catering, activePackagePrices: new decimal[] { 9000m }),
            // not approved
            CreateBrowsableVendor(6, status: VerificationStatus.Pending, avgRating: 4.9m, city: "Cairo", categoryId: 1, category: catering, activePackagePrices: new decimal[] { 800m })
        };
        SetupVendorQueryable(vendors);

        var filter = new VendorBrowseFilterDto
        {
            Category = "catering",
            City = "cairo",
            MinRating = 4.0m,
            MinPrice = 100m,
            MaxPrice = 1000m
        };

        var result = await CreateService().BrowseVendorsAsync(filter);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.Items.Single().Id);
    }

    [Fact]
    public async Task BrowseVendorsAsync_DefaultSort_TrustedFirstThenByRatingDescending()
    {
        var vendors = new List<Vendor>
        {
            CreateBrowsableVendor(1, status: VerificationStatus.Verified, avgRating: 4.9m),
            CreateBrowsableVendor(2, status: VerificationStatus.Trusted, avgRating: 3.0m),
            CreateBrowsableVendor(3, status: VerificationStatus.Trusted, avgRating: 4.5m),
            CreateBrowsableVendor(4, status: VerificationStatus.Verified, avgRating: 4.95m)
        };
        SetupVendorQueryable(vendors);

        var result = await CreateService().BrowseVendorsAsync(new VendorBrowseFilterDto { PageSize = 50 });

        Assert.Equal(new[] { 3L, 2L, 4L, 1L }, result.Items.Select(i => i.Id));
    }

    [Theory]
    [InlineData("rating")]
    [InlineData("priceAsc")]
    [InlineData("priceDesc")]
    public async Task BrowseVendorsAsync_SupportsAlternateSortOptions(string sortBy)
    {
        var vendors = new List<Vendor>
        {
            CreateBrowsableVendor(1, avgRating: 4.0m, activePackagePrices: new decimal[] { 900m }),
            CreateBrowsableVendor(2, avgRating: 4.9m, activePackagePrices: new decimal[] { 300m })
        };
        SetupVendorQueryable(vendors);

        var result = await CreateService().BrowseVendorsAsync(new VendorBrowseFilterDto { SortBy = sortBy });

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task BrowseVendorsAsync_Pagination_ReturnsCorrectSliceAndTotalCount()
    {
        var vendors = Enumerable.Range(1, 25)
            .Select(id => CreateBrowsableVendor(id, avgRating: 4.0m))
            .ToList();
        SetupVendorQueryable(vendors);

        var page2 = await CreateService().BrowseVendorsAsync(new VendorBrowseFilterDto { Page = 2, PageSize = 10 });

        Assert.Equal(25, page2.TotalCount);
        Assert.Equal(2, page2.Page);
        Assert.Equal(10, page2.PageSize);
        Assert.Equal(10, page2.Items.Count);
    }

    [Fact]
    public async Task BrowseVendorsAsync_PageSizeAboveMax_ClampsToDefault()
    {
        var vendors = Enumerable.Range(1, 5).Select(id => CreateBrowsableVendor(id)).ToList();
        SetupVendorQueryable(vendors);

        var result = await CreateService().BrowseVendorsAsync(new VendorBrowseFilterDto { PageSize = 500 });

        Assert.Equal(12, result.PageSize);
    }
}
