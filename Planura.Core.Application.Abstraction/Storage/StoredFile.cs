namespace Planura.Core.Application.Abstraction.Storage
{
    public class StoredFile
    {
        public string StoredPath { get; set; } = null!;
        public string ContentType { get; set; } = null!;
        public string? PublicUrl { get; set; }
    }
}
