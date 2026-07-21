using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Planura.Core.Application.Abstraction.AttachementService;
using Planura.Shared.Errors.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planura.Infrastructure.AttachementService
{
    public class AttachmentService : IAttachmentService
    {

        private readonly List<string> _allowedExtentions = new() { ".png", ".jpg", ".jpeg" };
        private const int _allowedMaxSize = 2_097_152;

        // Separate allow-list for generated documents (contracts/agreements): PDFs, larger cap.
        private readonly List<string> _allowedDocumentExtensions = new() { ".pdf" };
        private const int _allowedDocumentMaxSize = 10_485_760; // 10 MB

        private readonly IWebHostEnvironment _env;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AttachmentService(IWebHostEnvironment env, IHttpContextAccessor httpContextAccessor)
        {
            _env = env;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<string?> UploadAsynce(IFormFile file, string folderName)
        {
            var extention = Path.GetExtension(file.FileName);

            if (!_allowedExtentions.Contains(extention))
                throw new BadRequestExeption($"File extension '{extention}' is not allowed. Allowed: {string.Join(", ", _allowedExtentions)}");

            if (file.Length > _allowedMaxSize)
                throw new BadRequestExeption("File size exceeds the maximum allowed size (2 MB).");

            var folderPath = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "images", folderName);
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var fileName = $"{Guid.NewGuid():N}{extention}";
            var filePath = Path.Combine(folderPath, fileName);

            using var fileStream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(fileStream);

            return $"images/{folderName}/{fileName}";
        }

        public async Task<string?> UploadGeneratedFileAsync(byte[] fileBytes, string fileExtension, string folderName)
        {
            if (fileBytes.Length == 0)
                throw new BadRequestExeption("Generated file is empty.");

            var folderPath = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "images", folderName);
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var fileName = $"{Guid.NewGuid():N}{fileExtension}";
            var filePath = Path.Combine(folderPath, fileName);

            await File.WriteAllBytesAsync(filePath, fileBytes);

            return $"images/{folderName}/{fileName}";
        }

        public string? ToAbsoluteUrl(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return null;

            var request = _httpContextAccessor.HttpContext?.Request;
            if (request is null)
                return relativePath;

            return $"{request.Scheme}://{request.Host}/{relativePath.TrimStart('/')}";
        }

        public bool Delete(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            var fullPath = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), filePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return true;
            }
            return false;
        }
    }

}
