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
    public class AdminVerificationService : IAdminVerificationService
    {
        private readonly AppDbContext _dbContext;
        private readonly IFileStorageService _fileStorageService;
        private readonly IEmailService _emailService;

        public AdminVerificationService(AppDbContext dbContext, IFileStorageService fileStorageService, IEmailService emailService)
        {
            _dbContext = dbContext;
            _fileStorageService = fileStorageService;
            _emailService = emailService;
        }

        public async Task<PagedResult<VendorApplicationSummaryDto>> GetPendingAsync(int page, int pageSize, CancellationToken ct = default)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

            var query = _dbContext.VerificationRequests
                .AsNoTracking()
                .Where(r => r.Status == VerificationStatus.Pending)
                .OrderBy(r => r.SubmittedAt);

            var totalCount = await query.CountAsync(ct);

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new VendorApplicationSummaryDto
                {
                    RequestId = r.Id,
                    VendorProfileId = r.VendorProfileId,
                    BusinessName = r.VendorProfile.BusinessName,
                    BusinessType = r.VendorProfile.BusinessType,
                    SubmittedAt = r.SubmittedAt
                })
                .ToListAsync(ct);

            return new PagedResult<VendorApplicationSummaryDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<VendorApplicationDetailsDto> GetDetailsAsync(Guid requestId, CancellationToken ct = default)
        {
            var request = await _dbContext.VerificationRequests
                .AsNoTracking()
                .Include(r => r.Documents)
                .Include(r => r.VendorProfile)
                    .ThenInclude(p => p.Category)
                .Include(r => r.VendorProfile)
                    .ThenInclude(p => p.Images)
                .FirstOrDefaultAsync(r => r.Id == requestId, ct)
                ?? throw new NotFoundExeption(nameof(VerificationRequest), requestId);

            var profile = request.VendorProfile;

            return new VendorApplicationDetailsDto
            {
                RequestId = request.Id,
                VendorProfileId = profile.Id,
                BusinessName = profile.BusinessName,
                BusinessDescription = profile.BusinessDescription,
                BusinessType = profile.BusinessType,
                CategoryName = profile.Category.Name,
                Location = new LocationDto
                {
                    City = profile.Location.City,
                    Area = profile.Location.Area,
                    AddressLine = profile.Location.AddressLine,
                    Latitude = profile.Location.Latitude,
                    Longitude = profile.Location.Longitude
                },
                SubmittedAt = request.SubmittedAt,
                Documents = request.Documents.Select(d => new VendorDocumentMetadataDto
                {
                    Id = d.Id,
                    DocumentType = d.DocumentType,
                    ContentType = d.ContentType,
                    UploadedAt = d.UploadedAt
                }).ToList(),
                ImageUrls = profile.Images
                    .Select(i => _fileStorageService.GetPublicUrl(i.StoredPath))
                    .Where(url => url is not null)
                    .Select(url => url!)
                    .ToList()
            };
        }

        public async Task<VendorStatusResponse> ApproveAsync(Guid requestId, Guid adminUserId, CancellationToken ct = default)
        {
            var (request, profile) = await LoadPendingRequestForReviewAsync(requestId, ct);

            request.Status = VerificationStatus.Approved;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewedByUserId = adminUserId;
            profile.Status = VerificationStatus.Approved;
            profile.UpdatedAt = DateTime.UtcNow;

            await SaveReviewDecisionAsync(ct);

            var user = await _dbContext.Users.FirstAsync(u => u.Id == profile.UserId, ct);
            await _emailService.SendAsync(
                user.Email!,
                "Your vendor application was approved",
                $"Hi {user.FullName}, congratulations — your Planura vendor application has been approved.",
                ct);

            return ToStatusResponse(profile, request);
        }

        public async Task<VendorStatusResponse> RejectAsync(Guid requestId, Guid adminUserId, RejectApplicationRequest rejectRequest, CancellationToken ct = default)
        {
            var (request, profile) = await LoadPendingRequestForReviewAsync(requestId, ct);

            request.Status = VerificationStatus.Rejected;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewedByUserId = adminUserId;
            request.DecisionReason = rejectRequest.Reason;
            profile.Status = VerificationStatus.Rejected;
            profile.UpdatedAt = DateTime.UtcNow;

            await SaveReviewDecisionAsync(ct);

            var user = await _dbContext.Users.FirstAsync(u => u.Id == profile.UserId, ct);
            await _emailService.SendAsync(
                user.Email!,
                "Your vendor application was rejected",
                $"Hi {user.FullName}, your Planura vendor application was rejected. Reason: {rejectRequest.Reason}. You may resubmit updated documents.",
                ct);

            return ToStatusResponse(profile, request);
        }

        public async Task<VerificationHistoryDto> GetVendorHistoryAsync(Guid vendorProfileId, CancellationToken ct = default)
        {
            var profile = await _dbContext.VendorProfiles
                .AsNoTracking()
                .Include(v => v.VerificationRequests)
                    .ThenInclude(r => r.Documents)
                .FirstOrDefaultAsync(v => v.Id == vendorProfileId, ct)
                ?? throw new NotFoundExeption(nameof(VendorProfile), vendorProfileId);

            return VendorVerificationService.ToHistoryDto(profile);
        }

        private async Task<(VerificationRequest Request, VendorProfile Profile)> LoadPendingRequestForReviewAsync(Guid requestId, CancellationToken ct)
        {
            var request = await _dbContext.VerificationRequests
                .Include(r => r.VendorProfile)
                .FirstOrDefaultAsync(r => r.Id == requestId, ct)
                ?? throw new NotFoundExeption(nameof(VerificationRequest), requestId);

            if (request.Status != VerificationStatus.Pending)
            {
                throw new BadRequestExeption("Only pending applications can be reviewed.");
            }

            return (request, request.VendorProfile);
        }

        private async Task SaveReviewDecisionAsync(CancellationToken ct)
        {
            try
            {
                await _dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // RowVersion mismatch — another admin reviewed this request first.
                throw new BadRequestExeption("This application was already reviewed by someone else. Please refresh and try again.");
            }
        }

        private static VendorStatusResponse ToStatusResponse(VendorProfile profile, VerificationRequest request) => new()
        {
            Status = profile.Status,
            SubmittedAt = request.SubmittedAt,
            ReviewedAt = request.ReviewedAt,
            LatestDecisionReason = request.DecisionReason
        };
    }
}
