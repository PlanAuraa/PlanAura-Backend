using Microsoft.EntityFrameworkCore;
using Planura.Core.Application.Abstraction.Notifications;
using Planura.Core.Application.Abstraction.Storage;
using Planura.Core.Application.Abstraction.Vendors;
using Planura.Core.Application.Abstraction.Vendors.Contracts;
using Planura.Core.Domain.Entities.Vendors;
using Planura.Core.Domain.Enums;
using Planura.Infrastructure.Persistence;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Vendors
{
    public class VendorVerificationService : IVendorVerificationService
    {
        private readonly AppDbContext _dbContext;
        private readonly IFileStorageService _fileStorageService;
        private readonly IEmailService _emailService;

        public VendorVerificationService(AppDbContext dbContext, IFileStorageService fileStorageService, IEmailService emailService)
        {
            _dbContext = dbContext;
            _fileStorageService = fileStorageService;
            _emailService = emailService;
        }

        public async Task<VendorStatusResponse> GetMyStatusAsync(Guid userId, CancellationToken ct = default)
        {
            var profile = await _dbContext.VendorProfiles
                .AsNoTracking()
                .Include(v => v.CurrentVerificationRequest)
                .FirstOrDefaultAsync(v => v.UserId == userId, ct)
                ?? throw new NotFoundExeption(nameof(VendorProfile), userId);

            return ToStatusResponse(profile, profile.CurrentVerificationRequest);
        }

        public async Task<VerificationHistoryDto> GetMyHistoryAsync(Guid userId, CancellationToken ct = default)
        {
            var profile = await _dbContext.VendorProfiles
                .AsNoTracking()
                .Include(v => v.VerificationRequests)
                    .ThenInclude(r => r.Documents)
                .FirstOrDefaultAsync(v => v.UserId == userId, ct)
                ?? throw new NotFoundExeption(nameof(VendorProfile), userId);

            return ToHistoryDto(profile);
        }

        public async Task<VendorStatusResponse> ResubmitAsync(Guid userId, VendorResubmitRequest request, CancellationToken ct = default)
        {
            var profile = await _dbContext.VendorProfiles
                .FirstOrDefaultAsync(v => v.UserId == userId, ct)
                ?? throw new NotFoundExeption(nameof(VendorProfile), userId);

            if (profile.Status != VerificationStatus.Rejected)
            {
                throw new BadRequestExeption("Resubmission is only allowed after your application has been rejected.");
            }

            VendorDocumentRequirements.Validate(profile.BusinessType, request.Documents);

            var storedFiles = new List<(string StoredPath, FileStorageArea Area)>();
            var newRequestId = Guid.NewGuid();

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                _dbContext.VerificationRequests.Add(new VerificationRequest
                {
                    Id = newRequestId,
                    VendorProfileId = profile.Id,
                    Status = VerificationStatus.Pending
                });

                foreach (var document in request.Documents)
                {
                    var stored = await _fileStorageService.SaveAsync(
                        document.File.Content, document.File.FileName, document.File.ContentType, FileStorageArea.PrivateDocuments, ct);
                    storedFiles.Add((stored.StoredPath, FileStorageArea.PrivateDocuments));

                    _dbContext.VendorDocuments.Add(new VendorDocument
                    {
                        Id = Guid.NewGuid(),
                        VerificationRequestId = newRequestId,
                        DocumentType = document.DocumentType,
                        StoredPath = stored.StoredPath,
                        ContentType = stored.ContentType
                    });
                }

                await _dbContext.SaveChangesAsync(ct);

                profile.Status = VerificationStatus.Pending;
                profile.CurrentVerificationRequestId = newRequestId;
                profile.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(ct);

                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);

                foreach (var (storedPath, area) in storedFiles)
                {
                    await _fileStorageService.DeleteAsync(storedPath, area, ct);
                }

                throw;
            }

            var user = await _dbContext.Users.FirstAsync(u => u.Id == userId, ct);
            await _emailService.SendAsync(
                user.Email!,
                "Your resubmitted vendor application was received",
                $"Hi {user.FullName}, we received your resubmitted verification documents. Your application is now pending review again.",
                ct);

            var newRequest = await _dbContext.VerificationRequests.AsNoTracking().FirstAsync(r => r.Id == newRequestId, ct);
            return ToStatusResponse(profile, newRequest);
        }

        private static VendorStatusResponse ToStatusResponse(VendorProfile profile, VerificationRequest? currentRequest) => new()
        {
            Status = profile.Status,
            SubmittedAt = currentRequest?.SubmittedAt ?? profile.CreatedAt,
            ReviewedAt = currentRequest?.ReviewedAt,
            LatestDecisionReason = currentRequest?.DecisionReason
        };

        internal static VerificationHistoryDto ToHistoryDto(VendorProfile profile) => new()
        {
            VendorProfileId = profile.Id,
            BusinessName = profile.BusinessName,
            Requests = profile.VerificationRequests
                .OrderByDescending(r => r.SubmittedAt)
                .Select(r => new VerificationRequestDto
                {
                    Id = r.Id,
                    Status = r.Status,
                    SubmittedAt = r.SubmittedAt,
                    ReviewedAt = r.ReviewedAt,
                    DecisionReason = r.DecisionReason,
                    Documents = r.Documents.Select(d => new VendorDocumentMetadataDto
                    {
                        Id = d.Id,
                        DocumentType = d.DocumentType,
                        ContentType = d.ContentType,
                        UploadedAt = d.UploadedAt
                    }).ToList()
                }).ToList()
        };
    }
}
