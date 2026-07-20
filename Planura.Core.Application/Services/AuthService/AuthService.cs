using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Planura.Core.Application.Abstraction.AttachementService;
using Planura.Core.Application.Models;
using Planura.Core.Application.Models.Auth;
using Planura.Core.Application.Models.Emails;
using Planura.Core.Application.Services.Emails;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;
using System.Data;
using System.Security.Cryptography;

namespace Planura.Core.Application.Services.Auth;

public class AuthService : IAuthService
{
    private const string VendorVerificationDocumentsFolder = "vendor-verification-documents";
    private const string VendorPortfolioFolder = "vendor-portfolio";
    private const string PortfolioMediaTypeImage = "image";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAttachmentService _attachmentService;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ICurrentUserService currentUserService,
        IAttachmentService attachmentService,
        INotificationService notificationService,
        IEmailService emailService)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _currentUserService = currentUserService;
        _attachmentService = attachmentService;
        _notificationService = notificationService;
        _emailService = emailService;
    }

    public async Task<AuthResponseDto> RegisterClientAsync(RegisterClientDto dto)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName,
                PhoneNumber = dto.PhoneNumber
            };

            var created = await _userManager.CreateAsync(user, dto.Password);
            if (!created.Succeeded)
            {
                throw new BadRequestExeption(DescribeErrors(created));
            }

            var roleAssigned = await _userManager.AddToRoleAsync(user, Roles.Client);
            if (!roleAssigned.Succeeded)
            {
                throw new BadRequestExeption(DescribeErrors(roleAssigned));
            }

            var client = new Planura.Core.Domain.Entities.Client { UserId = user.Id };
            await _unitOfWork.Repository<Planura.Core.Domain.Entities.Client, long>().AddAsync(client);

            await _unitOfWork.CommitTransactionAsync();

            var roles = await _userManager.GetRolesAsync(user);
            return BuildAuthResponse(user, roles);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, dto.Password))
        {
            throw new UnAuthorizedExeption("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            throw new UnAuthorizedExeption("This account has been suspended.");
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);

        long? vendorId = null;
        if (roles.Contains(Roles.Vendor))
        {
            var vendor = await _unitOfWork.Repository<Planura.Core.Domain.Entities.Vendor, long>()
                .GetWithSpecAsync(new VendorByUserIdSpecification(user.Id));
            vendorId = vendor?.Id;
        }

        return BuildAuthResponse(user, roles, vendorId);
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync()
    {
        var userId = _currentUserService.UserId
            ?? throw new UnAuthorizedExeption("No authenticated user.");

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new NotFoundExeption(nameof(ApplicationUser), userId);
        }

        var roles = await _userManager.GetRolesAsync(user);

        return new CurrentUserDto
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            PhoneNumber = user.PhoneNumber,
            Roles = roles.ToList()
        };
    }

    public async Task<CurrentUserDto> UpdateMyProfileAsync(UpdateProfileDto dto)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnAuthorizedExeption("No authenticated user.");

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new NotFoundExeption(nameof(ApplicationUser), userId);
        }

        user.FullName = dto.FullName;
        user.PhoneNumber = dto.PhoneNumber;
        if (!string.IsNullOrWhiteSpace(dto.PreferredLanguage))
        {
            user.PreferredLanguage = dto.PreferredLanguage;
        }
        user.UpdatedAt = DateTimeOffset.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new BadRequestExeption(DescribeErrors(result));
        }

        var roles = await _userManager.GetRolesAsync(user);

        return new CurrentUserDto
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            PhoneNumber = user.PhoneNumber,
            Roles = roles.ToList()
        };
    }

    public async Task ChangePasswordAsync(ChangePasswordDto dto)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnAuthorizedExeption("No authenticated user.");

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new NotFoundExeption(nameof(ApplicationUser), userId);
        }

        var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!result.Succeeded)
        {
            throw new BadRequestExeption(DescribeErrors(result));
        }
    }

    private AuthResponseDto BuildAuthResponse(ApplicationUser user, IList<string> roles, long? vendorId = null)
    {
        var token = _tokenService.CreateToken(user, roles.ToList(), vendorId);

        return new AuthResponseDto
        {
            AccessToken = token.AccessToken,
            ExpiresAtUtc = token.ExpiresAtUtc,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            Roles = roles.ToList()
        };
    }
    // vendor
    public async Task<AuthResponseDto> RegisterVendorAsync(RegisterVendorDto dto)
    {
        await ValidateVendorRegistrationAsync(dto);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName,
                PhoneNumber = dto.PhoneNumber
            };

            var created = await _userManager.CreateAsync(user, dto.Password);
            if (!created.Succeeded)
            {
                throw new BadRequestExeption(DescribeErrors(created));
            }

            var roleAssigned = await _userManager.AddToRoleAsync(user, Roles.Vendor);
            if (!roleAssigned.Succeeded)
            {
                throw new BadRequestExeption(DescribeErrors(roleAssigned));
            }

            var vendor = new Vendor
            {
                UserId = user.Id,
                BusinessName = dto.BusinessName,
                BusinessDescription = dto.BusinessDescription,
                CategoryId = dto.CategoryId,
                City = dto.City,
                Address = dto.Address,
                VendorType = dto.VendorType,
                VerificationStatus = VerificationStatus.Pending
            };
            await _unitOfWork.Repository<Planura.Core.Domain.Entities.Vendor, long>().AddAsync(vendor);

            var verification = new Planura.Core.Domain.Entities.VendorVerification
            {
                Vendor = vendor,
                Status = VerificationStatus.Pending,
                SubmittedAt = DateTimeOffset.UtcNow,
                IsCurrent = true
            };
            await _unitOfWork.Repository<Planura.Core.Domain.Entities.VendorVerification, long>().AddAsync(verification);

            await AddVerificationDocumentAsync(verification, dto.NationalIdFront, VerificationDocumentType.NationalIdFront);
            await AddVerificationDocumentAsync(verification, dto.NationalIdBack, VerificationDocumentType.NationalIdBack);
            await AddVerificationDocumentAsync(verification, dto.SelfieWithId, VerificationDocumentType.SelfieWithId);

            if (dto.VendorType == VendorType.Business)
            {
                await AddVerificationDocumentAsync(verification, dto.CommercialRegistration!, VerificationDocumentType.CommercialRegistration);
                await AddVerificationDocumentAsync(verification, dto.TaxCard!, VerificationDocumentType.TaxCard);
            }

            await AddPortfolioMediaAsync(vendor, dto.PortfolioImages);

            await _unitOfWork.CommitTransactionAsync();

            await NotifyVendorSubmittedAsync(user.Id, vendor.BusinessName);

            var roles = await _userManager.GetRolesAsync(user);
            return BuildAuthResponse(user, roles, vendor.Id);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    private async Task NotifyVendorSubmittedAsync(long vendorUserId, string businessName)
    {
        // Notifications are best-effort: the registration has already been committed,
        // so a notification failure must never surface as a registration failure.
        try
        {
            await _notificationService.NotifyUserAsync(
                vendorUserId,
                NotificationTypes.VendorSubmitted,
                "Verification submitted",
                "Your documents were submitted and are pending review.");

            await _notificationService.NotifyRoleAsync(
                Roles.Admin,
                NotificationTypes.VendorPendingReview,
                "New vendor pending review",
                $"{businessName} submitted verification documents and is awaiting review.");
        }
        catch
        {
            // Swallow: notification delivery is not part of the registration contract.
        }
    }

    private async Task ValidateVendorRegistrationAsync(RegisterVendorDto dto)
    {
        if (!Enum.IsDefined(typeof(VendorType), dto.VendorType))
        {
            throw new BadRequestExeption("Invalid vendor type.");
        }

        if (dto.CategoryId is not null)
        {
            var category = await _unitOfWork.Repository<ServiceCategory, long>().GetAsync(dto.CategoryId.Value);
            if (category is null)
            {
                throw new NotFoundExeption(nameof(ServiceCategory), dto.CategoryId.Value);
            }
        }

        if (dto.VendorType == VendorType.Business)
        {
            if (dto.CommercialRegistration is null || dto.CommercialRegistration.Length == 0)
            {
                throw new BadRequestExeption("Commercial registration document is required for business vendors.");
            }

            if (dto.TaxCard is null || dto.TaxCard.Length == 0)
            {
                throw new BadRequestExeption("Tax card document is required for business vendors.");
            }
        }

        if (dto.NationalIdFront is null || dto.NationalIdFront.Length == 0)
        {
            throw new BadRequestExeption("National ID (front) photo is required.");
        }

        if (dto.NationalIdBack is null || dto.NationalIdBack.Length == 0)
        {
            throw new BadRequestExeption("National ID (back) photo is required.");
        }

        if (dto.SelfieWithId is null || dto.SelfieWithId.Length == 0)
        {
            throw new BadRequestExeption("Selfie with ID photo is required.");
        }

        if (dto.PortfolioImages is null || dto.PortfolioImages.Count == 0)
        {
            throw new BadRequestExeption("At least one portfolio image is required.");
        }
    }

    private async Task AddVerificationDocumentAsync(
        Planura.Core.Domain.Entities.VendorVerification verification,
        IFormFile file,
        VerificationDocumentType documentType)
    {
        var fileUrl = await _attachmentService.UploadAsynce(file, VendorVerificationDocumentsFolder);

        var document = new VendorVerificationDocument
        {
            VendorVerification = verification,
            DocumentType = documentType,
            FileUrl = fileUrl!,
            OriginalFileName = file.FileName,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length
        };

        await _unitOfWork.Repository<VendorVerificationDocument, long>().AddAsync(document);
    }

    private async Task AddPortfolioMediaAsync(Vendor vendor, List<IFormFile> images)
    {
        var repository = _unitOfWork.Repository<PortfolioMedia, long>();

        for (var index = 0; index < images.Count; index++)
        {
            var image = images[index];
            var fileUrl = await _attachmentService.UploadAsynce(image, VendorPortfolioFolder);

            var media = new PortfolioMedia
            {
                Vendor = vendor,
                MediaType = PortfolioMediaTypeImage,
                FileUrl = fileUrl!,
                Title = image.FileName,
                FileSizeKb = (int)(image.Length / 1024),
                DisplayOrder = index
            };

            await repository.AddAsync(media);
        }
    }

    public async Task<SuccessDto> ForgetPasswordByEmailasync(ForgetPasswordByEmailDto emailDto)
    {
        var user = await _userManager.Users.Where(u => u.Email == emailDto.Email).FirstOrDefaultAsync();

        if (user is null)
            throw new BadRequestExeption("Invalid Email");

        var ResetCode = RandomNumberGenerator.GetInt32(100_000, 999_999);

        var ResetCodeExpire = DateTime.UtcNow.AddMinutes(15);

        user.ResetCode = ResetCode;
        user.ResetCodeExpiry = ResetCodeExpire;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
            throw new BadRequestExeption("Something Went Wrong While Sending Reset Code");

        var email = new Email()
        {
            To = emailDto.Email,
            Subject = "Reset Code For PlanAura Account",
            Body = $"We Have Recived Your Request For Reset Your Account Password, \nYour Reset Code Is ==> [ {ResetCode} ] <== \nNote: This Code Will Be Expired After 15 Minutes!",
        };

        await _emailService.SendEmail(email);

        var SuccessObj = new SuccessDto()
        {
            Status = "Success",
            Message = "We Have Sent You The Reset Code"
        };

        return SuccessObj;
    }


    public async Task<SuccessDto> VerifyCodeByEmailAsync(ResetCodeConfirmationByEmailDto resetCodeDto)
    {
        var user = await _userManager.Users.Where(u => u.Email == resetCodeDto.Email).FirstOrDefaultAsync();

        if (user is null)
            throw new BadRequestExeption("Invalid Email");

        if (user.ResetCode != resetCodeDto.ResetCode)
            throw new BadRequestExeption("The Provided Code Is Invalid");

        if (user.ResetCodeExpiry < DateTime.UtcNow)
            throw new BadRequestExeption("The Provided Code Has Been Expired");

        var SuccessObj = new SuccessDto()
        {
            Status = "Success",
            Message = "Reset Code Is Verified, Please Proceed To Change Your Password"
        };

        return SuccessObj;
    }
    public async Task<AuthResponseDto> ResetPasswordByEmailAsync(ResetPasswordByEmailDto resetCodeDto)
    {
        var user = await _userManager.Users.Where(u => u.Email == resetCodeDto.Email).FirstOrDefaultAsync();

        if (user is null)
            throw new BadRequestExeption("Invalid Email");

        var RemovePass = await _userManager.RemovePasswordAsync(user);

        if (!RemovePass.Succeeded)
            throw new BadRequestExeption("Something Went Wrong While Reseting Your Password");

        var newPass = await _userManager.AddPasswordAsync(user, resetCodeDto.NewPassword);

        if (!newPass.Succeeded)
            throw new BadRequestExeption("Something Went Wrong While Reseting Your Password");

        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokenService.CreateToken(user, roles.ToList());

        var mappedUser = new AuthResponseDto
        {
            AccessToken = token.AccessToken,
            ExpiresAtUtc = token.ExpiresAtUtc,
            UserId = user.Id,
            FullName = user.FullName!,
            Email = user.Email!,
            Roles = roles.ToList()
        };



        return mappedUser;
    }
    private static string DescribeErrors(IdentityResult result)
        => string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
}
