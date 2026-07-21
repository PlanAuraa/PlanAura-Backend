using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Planura.Core.Application.Abstraction.AttachementService;
using Planura.Core.Application.Abstraction.WhatsApp;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;
namespace Planura.Core.Application.Services;
using Planura.Core.Application.Models.VendorVerification;
using Planura.Core.Application.Specifications.VendorVerification;

public class VendorVerificationService : IVendorVerificationService
{
    private const string VendorVerificationDocumentsFolder = "vendor-verification-documents";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAttachmentService _attachmentService;
    private readonly INotificationService _notificationService;
    private readonly IWhatsAppService _whatsAppService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<VendorVerificationService> _logger;

    public VendorVerificationService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IAttachmentService attachmentService,
        INotificationService notificationService,
        IWhatsAppService whatsAppService,
        UserManager<ApplicationUser> userManager,
        ILogger<VendorVerificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _attachmentService = attachmentService;
        _notificationService = notificationService;
        _whatsAppService = whatsAppService;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task ApproveVendorAsync(long vendorId)
    {
        await _unitOfWork.BeginTransactionAsync();

        try
        {
            var vendorRepository = _unitOfWork.Repository<Vendor, long>();
            var verificationRepository = _unitOfWork.Repository<VendorVerification, long>();
            var historyRepository = _unitOfWork.Repository<VendorVerificationHistory, long>();

            var vendor = await vendorRepository.GetAsync(vendorId);

            if (vendor is null)
            {
                throw new NotFoundExeption(nameof(Vendor), vendorId);
            }

            var verification = await verificationRepository
                .GetWithSpecAsync(new CurrentVendorVerificationSpecification(vendorId));

            if (verification is null)
            {
                throw new NotFoundExeption(nameof(VendorVerification), vendorId);
            }

            var previousStatus = verification.Status;

            vendor.VerificationStatus = VerificationStatus.Verified;

            verification.Status = VerificationStatus.Verified;
            verification.ReviewedAt = DateTimeOffset.UtcNow;
            verification.ReviewedByAdminId = _currentUserService.UserId;

            vendorRepository.Update(vendor);
            verificationRepository.Update(verification);

            var history = new VendorVerificationHistory
            {
                VendorVerificationId = verification.Id,
                PreviousStatus = previousStatus,
                NewStatus = VerificationStatus.Verified,
                ChangedByAdminId = _currentUserService.UserId,
                ChangedAt = DateTimeOffset.UtcNow,
                Notes = "Vendor approved."
            };

            await historyRepository.AddAsync(history);

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            await NotifyVendorDecisionAsync(
                vendor.UserId,
                NotificationTypes.VendorApproved,
                "Vendor approved",
                $"Your vendor account \"{vendor.BusinessName}\" has been approved.",
                $"Congratulations! Your vendor account \"{vendor.BusinessName}\" has been approved. You can now access your Vendor Dashboard on Planura.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task RejectVendorAsync(long vendorId, string rejectionReason)
    {
        await _unitOfWork.BeginTransactionAsync();

        try
        {
            var vendorRepository = _unitOfWork.Repository<Vendor, long>();
            var verificationRepository = _unitOfWork.Repository<VendorVerification, long>();
            var historyRepository = _unitOfWork.Repository<VendorVerificationHistory, long>();

            var vendor = await vendorRepository.GetAsync(vendorId);

            if (vendor is null)
            {
                throw new NotFoundExeption(nameof(Vendor), vendorId);
            }

            var verification = await verificationRepository
                .GetWithSpecAsync(new CurrentVendorVerificationSpecification(vendorId));

            if (verification is null)
            {
                throw new NotFoundExeption(nameof(VendorVerification), vendorId);
            }

            var previousStatus = verification.Status;

            vendor.VerificationStatus = VerificationStatus.Rejected;

            verification.Status = VerificationStatus.Rejected;
            verification.RejectionReason = rejectionReason;
            verification.ReviewedAt = DateTimeOffset.UtcNow;
            verification.ReviewedByAdminId = _currentUserService.UserId;

            vendorRepository.Update(vendor);
            verificationRepository.Update(verification);

            var history = new VendorVerificationHistory
            {
                VendorVerificationId = verification.Id,
                PreviousStatus = previousStatus,
                NewStatus = VerificationStatus.Rejected,
                ChangedByAdminId = _currentUserService.UserId,
                ChangedAt = DateTimeOffset.UtcNow,
                Notes = rejectionReason
            };

            await historyRepository.AddAsync(history);

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            await NotifyVendorDecisionAsync(
                vendor.UserId,
                NotificationTypes.VendorRejected,
                "Vendor verification rejected",
                $"Your vendor verification for \"{vendor.BusinessName}\" was not approved. Reason: {rejectionReason}",
                $"Your vendor verification for \"{vendor.BusinessName}\" was not approved. Reason: {rejectionReason}. You may resubmit your documents from your dashboard.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task PromoteToTrustedAsync(long vendorId)
    {
        await _unitOfWork.BeginTransactionAsync();

        try
        {
            var vendorRepository = _unitOfWork.Repository<Vendor, long>();
            var verificationRepository = _unitOfWork.Repository<VendorVerification, long>();
            var historyRepository = _unitOfWork.Repository<VendorVerificationHistory, long>();

            var vendor = await vendorRepository.GetAsync(vendorId);

            if (vendor is null)
            {
                throw new NotFoundExeption(nameof(Vendor), vendorId);
            }

            var verification = await verificationRepository
                .GetWithSpecAsync(new CurrentVendorVerificationSpecification(vendorId));

            if (verification is null)
            {
                throw new NotFoundExeption(nameof(VendorVerification), vendorId);
            }

            if (verification.Status != VerificationStatus.Verified)
            {
                throw new BadRequestExeption(
                    $"Only a verified vendor can be promoted to trusted (current status: '{verification.Status}').");
            }

            var previousStatus = verification.Status;

            vendor.VerificationStatus = VerificationStatus.Trusted;

            verification.Status = VerificationStatus.Trusted;
            verification.TrustedSince = DateTimeOffset.UtcNow;
            verification.ReviewedAt = DateTimeOffset.UtcNow;
            verification.ReviewedByAdminId = _currentUserService.UserId;

            vendorRepository.Update(vendor);
            verificationRepository.Update(verification);

            var history = new VendorVerificationHistory
            {
                VendorVerificationId = verification.Id,
                PreviousStatus = previousStatus,
                NewStatus = VerificationStatus.Trusted,
                ChangedByAdminId = _currentUserService.UserId,
                ChangedAt = DateTimeOffset.UtcNow,
                Notes = "Vendor promoted to trusted."
            };

            await historyRepository.AddAsync(history);

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<IEnumerable<PendingVendorDto>> GetPendingVendorsAsync()
    {
        var verificationRepository = _unitOfWork.Repository<VendorVerification, long>();

        var pendingVerifications = await verificationRepository
            .GetAllWithSpecAsync(new PendingVendorVerificationsSpecification());

        return pendingVerifications.Select(v => new PendingVendorDto
        {
            VendorId = v.VendorId,
            VendorName = v.Vendor.User.FullName,
            BusinessName = v.Vendor.BusinessName,
            VendorType = v.Vendor.VendorType,
            CategoryName = v.Vendor.Category?.NameEn,
            SubmittedAt = v.SubmittedAt,
            Status = v.Status
        });
    }

    public async Task<VendorDetailsDto> GetVendorDetailsAsync(long vendorId)
    {
        var verification = await _unitOfWork
            .Repository<VendorVerification, long>()
            .GetWithSpecAsync(new VendorVerificationDetailsSpecification(vendorId));

        if (verification is null)
        {
            throw new NotFoundExeption(nameof(VendorVerification), vendorId);
        }

        var vendor = verification.Vendor;

        return new VendorDetailsDto
        {
            VendorId = vendor.Id,
            UserId = vendor.UserId,
            VendorName = vendor.User.FullName,
            Email = vendor.User.Email,
            PhoneNumber = vendor.User.PhoneNumber,
            BusinessName = vendor.BusinessName,
            BusinessDescription = vendor.BusinessDescription,
            VendorType = vendor.VendorType,
            CategoryName = vendor.Category?.NameEn,
            City = vendor.City,
            Address = vendor.Address,

            VerificationStatus = verification.Status,
            SubmittedAt = verification.SubmittedAt,
            ReviewedAt = verification.ReviewedAt,
            RejectionReason = verification.RejectionReason,
            TrustedSince = verification.TrustedSince,

            AvgRating = vendor.AvgRating,
            TotalReviews = vendor.TotalReviews,
            TotalCompletedBookings = vendor.TotalCompletedBookings,

            Documents = verification.Documents
                .Select(d => new VendorDocumentDto
                {
                    DocumentType = d.DocumentType,
                    FileUrl = d.FileUrl,
                    OriginalFileName = d.OriginalFileName,
                    ContentType = d.ContentType,
                    FileSizeBytes = d.FileSizeBytes
                })
                .ToList(),

            PortfolioMedia = vendor.PortfolioMedia
                .OrderBy(m => m.DisplayOrder)
                .Select(m => new PortfolioMediaDto
                {
                    FileUrl = m.FileUrl,
                    Title = m.Title,
                    DisplayOrder = m.DisplayOrder
                })
                .ToList(),

            PartnershipAgreementId = vendor.PartnershipAgreementId,
            PartnershipAgreementUrl = vendor.PartnershipAgreementUrl,
            PartnershipAgreementGeneratedAt = vendor.PartnershipAgreementGeneratedAt
        };
    }

    public async Task<IEnumerable<VendorVerificationHistoryDto>> GetVerificationHistoryAsync(long vendorId)
    {
        var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(vendorId);

        if (vendor is null)
        {
            throw new NotFoundExeption(nameof(Vendor), vendorId);
        }

        var historyRepository = _unitOfWork.Repository<VendorVerificationHistory, long>();

        var history = await historyRepository
            .GetAllWithSpecAsync(new VendorVerificationHistoryByVendorSpecification(vendorId));

        return history.Select(h => new VendorVerificationHistoryDto
        {
            VendorVerificationId = h.VendorVerificationId,
            PreviousStatus = h.PreviousStatus,
            NewStatus = h.NewStatus,
            ChangedByAdminName = h.ChangedByAdmin?.FullName,
            Notes = h.Notes,
            ChangedAt = h.ChangedAt
        });
    }

    public async Task<VendorVerificationStatusDto> ResubmitVerificationAsync(long vendorId, ResubmitVerificationDto dto)
    {
        var vendorRepository = _unitOfWork.Repository<Vendor, long>();
        var vendor = await vendorRepository.GetAsync(vendorId);

        if (vendor is null)
        {
            throw new NotFoundExeption(nameof(Vendor), vendorId);
        }

        var verificationRepository = _unitOfWork.Repository<VendorVerification, long>();
        var currentVerification = await verificationRepository
            .GetWithSpecAsync(new CurrentVendorVerificationSpecification(vendorId));

        if (currentVerification is null)
        {
            throw new NotFoundExeption(nameof(VendorVerification), vendorId);
        }

        if (currentVerification.Status != VerificationStatus.Rejected)
        {
            throw new BadRequestExeption("Only a rejected verification can be resubmitted.");
        }

        ValidateResubmissionDocuments(vendor.VendorType, dto);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            currentVerification.IsCurrent = false;
            verificationRepository.Update(currentVerification);

            var newVerification = new VendorVerification
            {
                Vendor = vendor,
                Status = VerificationStatus.Pending,
                SubmittedAt = DateTimeOffset.UtcNow,
                IsCurrent = true
            };
            await verificationRepository.AddAsync(newVerification);

            await AddVerificationDocumentAsync(newVerification, dto.NationalIdFront, VerificationDocumentType.NationalIdFront);
            await AddVerificationDocumentAsync(newVerification, dto.NationalIdBack, VerificationDocumentType.NationalIdBack);
            await AddVerificationDocumentAsync(newVerification, dto.SelfieWithId, VerificationDocumentType.SelfieWithId);

            if (vendor.VendorType == VendorType.Business)
            {
                await AddVerificationDocumentAsync(newVerification, dto.CommercialRegistration!, VerificationDocumentType.CommercialRegistration);
                await AddVerificationDocumentAsync(newVerification, dto.TaxCard!, VerificationDocumentType.TaxCard);
            }

            vendor.VerificationStatus = VerificationStatus.Pending;
            vendorRepository.Update(vendor);

            var history = new VendorVerificationHistory
            {
                VendorVerification = newVerification,
                PreviousStatus = VerificationStatus.Rejected,
                NewStatus = VerificationStatus.Pending,
                ChangedByAdminId = null,
                ChangedAt = DateTimeOffset.UtcNow,
                Notes = "Vendor resubmitted."
            };
            await _unitOfWork.Repository<VendorVerificationHistory, long>().AddAsync(history);

            await _unitOfWork.CommitTransactionAsync();

            return new VendorVerificationStatusDto
            {
                VendorVerificationId = newVerification.Id,
                Status = newVerification.Status,
                SubmittedAt = newVerification.SubmittedAt
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    private static void ValidateResubmissionDocuments(VendorType vendorType, ResubmitVerificationDto dto)
    {
        if (vendorType == VendorType.Business)
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
    }

    private async Task AddVerificationDocumentAsync(
        VendorVerification verification,
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

    private async Task NotifyVendorDecisionAsync(
        long userId, string notificationType, string title, string body, string whatsAppMessage)
    {
        try
        {
            await _notificationService.NotifyUserAsync(userId, notificationType, title, body);

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (!string.IsNullOrWhiteSpace(user?.PhoneNumber))
            {
                await _whatsAppService.SendMessageAsync(user.PhoneNumber, whatsAppMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify vendor user {UserId} of a verification decision", userId);
        }
    }
}