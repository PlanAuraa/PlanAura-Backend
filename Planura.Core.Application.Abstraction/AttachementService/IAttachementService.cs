using Microsoft.AspNetCore.Http;

namespace Planura.Core.Application.Abstraction.AttachementService
{
    public interface IAttachmentService
    {
        Task<string?> UploadAsynce(IFormFile file, string folderName);

        string? ToAbsoluteUrl(string? relativePath);

        bool Delete(string filePath);
    }
}
