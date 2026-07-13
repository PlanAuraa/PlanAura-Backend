using Microsoft.AspNetCore.Http;
using Moq;
using Planura.Core.Application.Abstraction.AttachementService;
using Planura.Core.Application.Models.Vendor;
using Planura.Core.Application.Services;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;
using Planura.Tests.TestHelpers;
using Xunit;

namespace Planura.Tests.Services;

public class VendorServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IAttachmentService> _attachmentServiceMock = new();

    private VendorService CreateService() => new(_unitOfWorkMock.Object, _attachmentServiceMock.Object);

    private static Vendor CreateVendor(long id = 1) => new()
    {
        Id = id,
        UserId = 100 + id,
        BusinessName = "Test Business"
    };

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
}
