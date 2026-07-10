using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Planura.Core.Application.Abstraction.Authentication;
using Planura.Core.Application.Abstraction.Authentication.Contracts;
using Planura.Core.Application.Abstraction.Notifications;
using Planura.Core.Application.Abstraction.Storage;
using Planura.Core.Application.Abstraction.Vendors;
using Planura.Core.Application.Abstraction.Vendors.Contracts;
using Planura.Core.Domain.Entities.Identity;
using Planura.Core.Domain.Entities.Vendors;
using Planura.Core.Domain.Enums;
using Planura.Infrastructure.Persistence;
using Planura.Shared.Constants;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Vendors
{
    public class VendorOnboardingService : IVendorOnboardingService
    {
        private readonly AppDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileStorageService _fileStorageService;
        private readonly IJwtTokenGenerator _jwtTokenGenerator;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly IEmailService _emailService;

        public VendorOnboardingService(
            AppDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            IFileStorageService fileStorageService,
            IJwtTokenGenerator jwtTokenGenerator,
            IRefreshTokenService refreshTokenService,
            IEmailService emailService)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _fileStorageService = fileStorageService;
            _jwtTokenGenerator = jwtTokenGenerator;
            _refreshTokenService = refreshTokenService;
            _emailService = emailService;
        }

        public async Task<AuthResponse> RegisterAsync(VendorRegisterRequest request, string? ipAddress, CancellationToken ct = default)
        {
            VendorDocumentRequirements.Validate(request.BusinessInfo.BusinessType, request.Documents);

            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser is not null)
            {
                throw new BadRequestExeption("An account with this email already exists.");
            }

            var categoryExists = await _dbContext.VendorCategories
                .AnyAsync(c => c.Id == request.BusinessInfo.CategoryId && c.IsActive, ct);
            if (!categoryExists)
            {
                throw new BadRequestExeption("The selected vendor category is invalid.");
            }

            var storedFiles = new List<(string StoredPath, FileStorageArea Area)>();
            ApplicationUser user;

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                user = new ApplicationUser
                {
                    UserName = request.Email,
                    Email = request.Email,
                    FullName = request.FullName,
                    PhoneNumber = request.PhoneNumber,
                    EmailConfirmed = true // email confirmation is deferred to a later phase
                };

                var createResult = await _userManager.CreateAsync(user, request.Password);
                if (!createResult.Succeeded)
                {
                    var errors = string.Join(" ", createResult.Errors.Select(e => e.Description));
                    throw new BadRequestExeption(errors);
                }

                await _userManager.AddToRoleAsync(user, Roles.Vendor);

                var profileId = Guid.NewGuid();
                var verificationRequestId = Guid.NewGuid();

                var vendorProfile = new VendorProfile
                {
                    Id = profileId,
                    UserId = user.Id,
                    BusinessName = request.BusinessInfo.BusinessName,
                    BusinessDescription = request.BusinessInfo.BusinessDescription,
                    BusinessType = request.BusinessInfo.BusinessType,
                    CategoryId = request.BusinessInfo.CategoryId,
                    Location = new VendorLocation
                    {
                        City = request.Location.City,
                        Area = request.Location.Area,
                        AddressLine = request.Location.AddressLine,
                        Latitude = request.Location.Latitude,
                        Longitude = request.Location.Longitude
                    },
                    Status = VerificationStatus.Pending
                    // CurrentVerificationRequestId is set below, after the mutually-referencing
                    // VerificationRequest row exists — inserting both FKs in the same batch makes
                    // EF Core's dependency graph report a circular-reference save failure.
                };
                _dbContext.VendorProfiles.Add(vendorProfile);

                _dbContext.VerificationRequests.Add(new VerificationRequest
                {
                    Id = verificationRequestId,
                    VendorProfileId = profileId,
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
                        VerificationRequestId = verificationRequestId,
                        DocumentType = document.DocumentType,
                        StoredPath = stored.StoredPath,
                        ContentType = stored.ContentType
                    });
                }

                var isFirstImage = true;
                foreach (var image in request.Images)
                {
                    var stored = await _fileStorageService.SaveAsync(
                        image.Content, image.FileName, image.ContentType, FileStorageArea.PublicImages, ct);
                    storedFiles.Add((stored.StoredPath, FileStorageArea.PublicImages));

                    _dbContext.VendorImages.Add(new VendorImage
                    {
                        Id = Guid.NewGuid(),
                        VendorProfileId = profileId,
                        StoredPath = stored.StoredPath,
                        ContentType = stored.ContentType,
                        IsPrimary = isFirstImage
                    });
                    isFirstImage = false;
                }

                await _dbContext.SaveChangesAsync(ct);

                vendorProfile.CurrentVerificationRequestId = verificationRequestId;
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

            var roles = await _userManager.GetRolesAsync(user);
            var accessToken = _jwtTokenGenerator.GenerateAccessToken(user.Id, user.Email!, user.FullName, roles);
            var refreshToken = await _refreshTokenService.IssueAsync(user.Id, ipAddress, ct);

            await _emailService.SendAsync(
                user.Email!,
                "Your vendor application was received",
                $"Hi {user.FullName}, thanks for applying to become a Planura vendor. Your application is now pending review.",
                ct);

            return new AuthResponse
            {
                AccessToken = accessToken.Token,
                AccessTokenExpiresAtUtc = accessToken.ExpiresAtUtc,
                RefreshToken = refreshToken.Token,
                RefreshTokenExpiresAtUtc = refreshToken.ExpiresAtUtc,
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email!,
                    FullName = user.FullName,
                    Roles = roles.ToArray()
                }
            };
        }
    }
}
