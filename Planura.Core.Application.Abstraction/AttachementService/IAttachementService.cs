using Microsoft.AspNetCore.Http;

namespace Planura.Core.Application.Abstraction.AttachementService
{
    public interface IAttachmentService
    {
        Task<string?> UploadAsynce(IFormFile file, string folderName);

        // Persists bytes that did not arrive as an IFormFile - e.g. an image
        // returned by an external AI provider - under the same wwwroot/images
        // layout as UploadAsynce. No extension allow-list/size cap: the
        // source is our own trusted provider call, not untrusted user input.
        Task<string?> UploadGeneratedFileAsync(byte[] fileBytes, string fileExtension, string folderName);

        string? ToAbsoluteUrl(string? relativePath);

        bool Delete(string filePath);
    }
}
