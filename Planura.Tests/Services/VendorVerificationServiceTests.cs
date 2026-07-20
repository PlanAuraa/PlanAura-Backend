using Microsoft.Extensions.Logging;
using Moq;
using Planura.Core.Application.Abstraction.AttachementService;
using Planura.Core.Application.Abstraction.WhatsApp;
using Planura.Core.Application.Models.VendorVerification;
using Planura.Core.Application.Services;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;
using Planura.Tests.TestHelpers;
using Xunit;

namespace Planura.Tests.Services;

public class VendorVerificationServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IAttachmentService> _attachmentServiceMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Mock<IWhatsAppService> _whatsAppServiceMock = new();
    private readonly Mock<ILogger<VendorVerificationService>> _loggerMock = new();

    private VendorVerificationService CreateService() => new(
         _unitOfWorkMock.Object,
         _currentUserServiceMock.Object,
         _attachmentServiceMock.Object,
         _notificationServiceMock.Object,
         _whatsAppServiceMock.Object,
         IdentityMockFactory.CreateUserManagerMock().Object,
         _loggerMock.Object);

    private static Vendor CreateVendor(long id = 1, VendorType type = VendorType.Individual) => new()
    {
        Id = id,
        UserId = 100 + id,
        BusinessName = "Test Business",
        VendorType = type,
        VerificationStatus = VerificationStatus.Pending
    };

    private static VendorVerification CreateVerification(long id, long vendorId, string status) => new()
    {
        Id = id,
        VendorId = vendorId,
        Status = status,
        IsCurrent = true,
        SubmittedAt = DateTimeOffset.UtcNow.AddDays(-1)
    };

    [Fact]
    public async Task ApproveVendorAsync_VendorNotFound_ThrowsNotFoundAndRollsBack()
    {
        var vendorRepo = _unitOfWorkMock.SetupRepository<Vendor, long>();
        vendorRepo.Setup(r => r.GetAsync(1)).ReturnsAsync((Vendor?)null);
        _unitOfWorkMock.SetupRepository<VendorVerification, long>();
        _unitOfWorkMock.SetupRepository<VendorVerificationHistory, long>();

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.ApproveVendorAsync(1));

        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveVendorAsync_NoCurrentVerification_ThrowsNotFound()
    {
        var vendor = CreateVendor();

        var vendorRepo = _unitOfWorkMock.SetupRepository<Vendor, long>();
        vendorRepo.Setup(r => r.GetAsync(vendor.Id)).ReturnsAsync(vendor);

        var verificationRepo = _unitOfWorkMock.SetupRepository<VendorVerification, long>();
        verificationRepo
            .Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorVerification>>()))
            .ReturnsAsync((VendorVerification?)null);

        _unitOfWorkMock.SetupRepository<VendorVerificationHistory, long>();

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.ApproveVendorAsync(vendor.Id));
    }

    [Fact]
    public async Task ApproveVendorAsync_Valid_SetsVerifiedStatusAndWritesHistory()
    {
        var vendor = CreateVendor();
        var verification = CreateVerification(id: 10, vendorId: vendor.Id, status: VerificationStatus.Pending);

        var vendorRepo = _unitOfWorkMock.SetupRepository<Vendor, long>();
        vendorRepo.Setup(r => r.GetAsync(vendor.Id)).ReturnsAsync(vendor);

        var verificationRepo = _unitOfWorkMock.SetupRepository<VendorVerification, long>();
        verificationRepo
            .Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorVerification>>()))
            .ReturnsAsync(verification);

        var historyRepo = _unitOfWorkMock.SetupRepository<VendorVerificationHistory, long>();
        VendorVerificationHistory? capturedHistory = null;
        historyRepo
            .Setup(r => r.AddAsync(It.IsAny<VendorVerificationHistory>()))
            .Callback<VendorVerificationHistory>(h => capturedHistory = h)
            .Returns(Task.CompletedTask);

        _currentUserServiceMock.Setup(c => c.UserId).Returns(99);

        var service = CreateService();
        await service.ApproveVendorAsync(vendor.Id);

        Assert.Equal(VerificationStatus.Verified, vendor.VerificationStatus);
        Assert.Equal(VerificationStatus.Verified, verification.Status);
        Assert.Equal(99, verification.ReviewedByAdminId);
        Assert.NotNull(capturedHistory);
        Assert.Equal(VerificationStatus.Pending, capturedHistory!.PreviousStatus);
        Assert.Equal(VerificationStatus.Verified, capturedHistory.NewStatus);

        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RejectVendorAsync_Valid_SetsRejectedStatusWithReason()
    {
        var vendor = CreateVendor();
        var verification = CreateVerification(id: 11, vendorId: vendor.Id, status: VerificationStatus.Pending);

        var vendorRepo = _unitOfWorkMock.SetupRepository<Vendor, long>();
        vendorRepo.Setup(r => r.GetAsync(vendor.Id)).ReturnsAsync(vendor);

        var verificationRepo = _unitOfWorkMock.SetupRepository<VendorVerification, long>();
        verificationRepo
            .Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorVerification>>()))
            .ReturnsAsync(verification);

        _unitOfWorkMock.SetupRepository<VendorVerificationHistory, long>();
        _currentUserServiceMock.Setup(c => c.UserId).Returns(99);

        var service = CreateService();
        await service.RejectVendorAsync(vendor.Id, "Documents are blurry.");

        Assert.Equal(VerificationStatus.Rejected, vendor.VerificationStatus);
        Assert.Equal(VerificationStatus.Rejected, verification.Status);
        Assert.Equal("Documents are blurry.", verification.RejectionReason);

        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResubmitVerificationAsync_CurrentStatusNotRejected_ThrowsBadRequestWithoutOpeningTransaction()
    {
        var vendor = CreateVendor();
        var verification = CreateVerification(id: 12, vendorId: vendor.Id, status: VerificationStatus.Pending);

        var vendorRepo = _unitOfWorkMock.SetupRepository<Vendor, long>();
        vendorRepo.Setup(r => r.GetAsync(vendor.Id)).ReturnsAsync(vendor);

        var verificationRepo = _unitOfWorkMock.SetupRepository<VendorVerification, long>();
        verificationRepo
            .Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorVerification>>()))
            .ReturnsAsync(verification);

        var dto = new ResubmitVerificationDto
        {
            NationalIdFront = FormFileFactory.Create(),
            NationalIdBack = FormFileFactory.Create(),
            SelfieWithId = FormFileFactory.Create()
        };

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.ResubmitVerificationAsync(vendor.Id, dto));

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResubmitVerificationAsync_BusinessVendorMissingCommercialRegistration_ThrowsBadRequest()
    {
        var vendor = CreateVendor(type: VendorType.Business);
        var verification = CreateVerification(id: 13, vendorId: vendor.Id, status: VerificationStatus.Rejected);

        var vendorRepo = _unitOfWorkMock.SetupRepository<Vendor, long>();
        vendorRepo.Setup(r => r.GetAsync(vendor.Id)).ReturnsAsync(vendor);

        var verificationRepo = _unitOfWorkMock.SetupRepository<VendorVerification, long>();
        verificationRepo
            .Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorVerification>>()))
            .ReturnsAsync(verification);

        var dto = new ResubmitVerificationDto
        {
            NationalIdFront = FormFileFactory.Create(),
            NationalIdBack = FormFileFactory.Create(),
            SelfieWithId = FormFileFactory.Create(),
            CommercialRegistration = null,
            TaxCard = FormFileFactory.Create()
        };

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.ResubmitVerificationAsync(vendor.Id, dto));
    }

    [Fact]
    public async Task ResubmitVerificationAsync_Valid_FlipsOldRowAndCreatesNewPendingRow()
    {
        var vendor = CreateVendor();
        var oldVerification = CreateVerification(id: 14, vendorId: vendor.Id, status: VerificationStatus.Rejected);

        var vendorRepo = _unitOfWorkMock.SetupRepository<Vendor, long>();
        vendorRepo.Setup(r => r.GetAsync(vendor.Id)).ReturnsAsync(vendor);

        var verificationRepo = _unitOfWorkMock.SetupRepository<VendorVerification, long>();
        verificationRepo
            .Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<VendorVerification>>()))
            .ReturnsAsync(oldVerification);

        VendorVerification? capturedNewVerification = null;
        verificationRepo
            .Setup(r => r.AddAsync(It.IsAny<VendorVerification>()))
            .Callback<VendorVerification>(v => capturedNewVerification = v)
            .Returns(Task.CompletedTask);

        _unitOfWorkMock.SetupRepository<VendorVerificationDocument, long>();
        _unitOfWorkMock.SetupRepository<VendorVerificationHistory, long>();

        _attachmentServiceMock
            .Setup(a => a.UploadAsynce(It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(), It.IsAny<string>()))
            .ReturnsAsync("images/vendor-verification-documents/fake.jpg");

        var dto = new ResubmitVerificationDto
        {
            NationalIdFront = FormFileFactory.Create(),
            NationalIdBack = FormFileFactory.Create(),
            SelfieWithId = FormFileFactory.Create()
        };

        var service = CreateService();
        var result = await service.ResubmitVerificationAsync(vendor.Id, dto);

        Assert.False(oldVerification.IsCurrent);
        Assert.Equal(VerificationStatus.Pending, vendor.VerificationStatus);
        Assert.NotNull(capturedNewVerification);
        Assert.Equal(VerificationStatus.Pending, capturedNewVerification!.Status);
        Assert.True(capturedNewVerification.IsCurrent);
        Assert.Equal(VerificationStatus.Pending, result.Status);

        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
