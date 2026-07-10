namespace Planura.Core.Application.Abstraction.Storage
{
    public interface IFileStorageService
    {
        Task<StoredFile> SaveAsync(Stream content, string fileName, string contentType, FileStorageArea area, CancellationToken ct = default);

        Task<Stream> OpenReadAsync(string storedPath, FileStorageArea area, CancellationToken ct = default);

        Task DeleteAsync(string storedPath, FileStorageArea area, CancellationToken ct = default);

        string? GetPublicUrl(string storedPath);
    }
}
