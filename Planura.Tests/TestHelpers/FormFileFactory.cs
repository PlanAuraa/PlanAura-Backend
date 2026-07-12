using System.Text;
using Microsoft.AspNetCore.Http;

namespace Planura.Tests.TestHelpers;

/// <summary>
/// Builds real <see cref="FormFile"/> instances (rather than mocking <see cref="IFormFile"/>)
/// so tests exercise the same "Length", "FileName", and "ContentType" reads the services rely on.
/// </summary>
public static class FormFileFactory
{
    public static IFormFile Create(
        string fileName = "photo.jpg",
        string contentType = "image/jpeg",
        int sizeBytes = 1024)
    {
        var bytes = Encoding.UTF8.GetBytes(new string('a', sizeBytes));
        var stream = new MemoryStream(bytes);

        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
