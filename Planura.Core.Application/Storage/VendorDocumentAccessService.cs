using Microsoft.EntityFrameworkCore;
using Planura.Core.Application.Abstraction.Storage;
using Planura.Core.Domain.Entities.Vendors;
using Planura.Infrastructure.Persistence;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Storage
{
    public class VendorDocumentAccessService : IVendorDocumentAccessService
    {
        private readonly AppDbContext _dbContext;
        private readonly IFileStorageService _fileStorageService;

        public VendorDocumentAccessService(AppDbContext dbContext, IFileStorageService fileStorageService)
        {
            _dbContext = dbContext;
            _fileStorageService = fileStorageService;
        }

        public async Task<VendorDocumentStreamResult> GetDocumentStreamAsync(Guid documentId, Guid currentUserId, bool isAdmin, CancellationToken ct = default)
        {
            var document = await _dbContext.VendorDocuments
                .AsNoTracking()
                .Include(d => d.VerificationRequest)
                    .ThenInclude(r => r.VendorProfile)
                .FirstOrDefaultAsync(d => d.Id == documentId, ct)
                ?? throw new NotFoundExeption(nameof(VendorDocument), documentId);

            var isOwner = document.VerificationRequest.VendorProfile.UserId == currentUserId;
            if (!isOwner && !isAdmin)
            {
                throw new ForbiddenExeption("You do not have access to this document.");
            }

            var stream = await _fileStorageService.OpenReadAsync(document.StoredPath, FileStorageArea.PrivateDocuments, ct);

            return new VendorDocumentStreamResult
            {
                Stream = stream,
                ContentType = document.ContentType,
                FileName = $"{document.DocumentType}-{document.Id:N}{GetExtension(document.ContentType)}"
            };
        }

        private static string GetExtension(string contentType) => contentType switch
        {
            "application/pdf" => ".pdf",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            _ => string.Empty
        };
    }
}
