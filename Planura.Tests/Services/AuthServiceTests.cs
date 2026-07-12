using Microsoft.AspNetCore.Identity;
using Moq;
using Planura.Core.Application.Abstraction.AttachementService;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;
using Planura.Tests.TestHelpers;
using Xunit;

namespace Planura.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock = IdentityMockFactory.CreateUserManagerMock();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IAttachmentService> _attachmentServiceMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();

    private AuthService CreateService() => new(
        _userManagerMock.Object,
        _unitOfWorkMock.Object,
        _tokenServiceMock.Object,
        _currentUserServiceMock.Object,
        _attachmentServiceMock.Object,
        _notificationServiceMock.Object);

    private static RegisterVendorDto CreateValidIndividualDto() => new()
    {
        FullName = "Jane Vendor",
        Email = "jane@example.com",
        PhoneNumber = "01000000000",
        Password = "P@ssw0rd123",
        ConfirmPassword = "P@ssw0rd123",
        BusinessName = "Jane's Events",
        VendorType = VendorType.Individual,
        NationalIdFront = FormFileFactory.Create("front.jpg"),
        NationalIdBack = FormFileFactory.Create("back.jpg"),
        SelfieWithId = FormFileFactory.Create("selfie.jpg"),
        PortfolioImages = new List<Microsoft.AspNetCore.Http.IFormFile> { FormFileFactory.Create("p1.jpg") }
    };

    // ---- Validation branches (must fail before any transaction/Identity work happens) ----

    [Fact]
    public async Task RegisterVendorAsync_InvalidVendorType_ThrowsBadRequestAndNeverOpensTransaction()
    {
        var dto = CreateValidIndividualDto();
        dto.VendorType = (VendorType)999;

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.RegisterVendorAsync(dto));

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterVendorAsync_CategoryDoesNotExist_ThrowsNotFound()
    {
        var dto = CreateValidIndividualDto();
        dto.CategoryId = 42;

        var categoryRepo = _unitOfWorkMock.SetupRepository<ServiceCategory, long>();
        categoryRepo.Setup(r => r.GetAsync(42)).ReturnsAsync((ServiceCategory?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.RegisterVendorAsync(dto));
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterVendorAsync_BusinessVendorMissingCommercialRegistration_ThrowsBadRequest()
    {
        var dto = CreateValidIndividualDto();
        dto.VendorType = VendorType.Business;
        dto.CommercialRegistration = null;
        dto.TaxCard = FormFileFactory.Create("tax.jpg");

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<BadRequestExeption>(() => service.RegisterVendorAsync(dto));
        Assert.Contains("Commercial registration", ex.Message);
    }

    [Fact]
    public async Task RegisterVendorAsync_BusinessVendorMissingTaxCard_ThrowsBadRequest()
    {
        var dto = CreateValidIndividualDto();
        dto.VendorType = VendorType.Business;
        dto.CommercialRegistration = FormFileFactory.Create("cr.jpg");
        dto.TaxCard = null;

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<BadRequestExeption>(() => service.RegisterVendorAsync(dto));
        Assert.Contains("Tax card", ex.Message);
    }

    [Fact]
    public async Task RegisterVendorAsync_MissingPortfolioImages_ThrowsBadRequest()
    {
        var dto = CreateValidIndividualDto();
        dto.PortfolioImages = new List<Microsoft.AspNetCore.Http.IFormFile>();

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<BadRequestExeption>(() => service.RegisterVendorAsync(dto));
        Assert.Contains("portfolio image", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Transactional behavior ----

    [Fact]
    public async Task RegisterVendorAsync_IdentityCreateFails_RollsBackAndNeverCommits()
    {
        var dto = CreateValidIndividualDto();

        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), dto.Password))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "DuplicateEmail", Description = "Email already taken." }));

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.RegisterVendorAsync(dto));

        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterVendorAsync_ValidIndividualVendor_CommitsAndReturnsAuthResponse()
    {
        var dto = CreateValidIndividualDto();

        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), dto.Password))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock
            .Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "vendor"))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock
            .Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(new List<string> { "vendor" });

        _attachmentServiceMock
            .Setup(a => a.UploadAsynce(It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(), It.IsAny<string>()))
            .ReturnsAsync("images/vendor-verification-documents/fake.jpg");

        _tokenServiceMock
            .Setup(t => t.CreateToken(It.IsAny<ApplicationUser>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<long?>()))
            .Returns(new JwtTokenResult { AccessToken = "fake-token", ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1) });

        _notificationServiceMock
            .Setup(n => n.NotifyUserAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        _notificationServiceMock
            .Setup(n => n.NotifyRoleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock.SetupRepository<Vendor, long>();
        _unitOfWorkMock.SetupRepository<VendorVerification, long>();
        _unitOfWorkMock.SetupRepository<VendorVerificationDocument, long>();
        _unitOfWorkMock.SetupRepository<PortfolioMedia, long>();

        var service = CreateService();

        var result = await service.RegisterVendorAsync(dto);

        Assert.NotNull(result);
        Assert.Equal(dto.Email, result.Email);
        Assert.Equal("fake-token", result.AccessToken);

        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
