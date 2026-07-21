namespace Planura.Core.Application.Abstraction.Contract
{
    /// <summary>
    /// Generates legal document text using Google's Gemini API. Deliberately generic (system
    /// instruction + prompt in, text out) so any Core.Application service can ask for a document
    /// draft without GeminiService needing to know what kind of contract it is.
    /// </summary>
    public interface IGeminiService
    {
        Task<string> GenerateTextAsync(GeminiTextGenerationRequest request, CancellationToken cancellationToken = default);
    }
}
