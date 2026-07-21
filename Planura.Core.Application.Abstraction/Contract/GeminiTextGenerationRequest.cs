namespace Planura.Core.Application.Abstraction.Contract
{
    /// <summary>
    /// A fully-formed prompt ready to send to Gemini. Callers (Core.Application services) own all
    /// prompt engineering / business content; GeminiService itself only knows how to talk HTTP to
    /// the Gemini API and return the raw text - it has no opinion about what kind of document is
    /// being drafted.
    /// </summary>
    public class GeminiTextGenerationRequest
    {
        public string SystemInstruction { get; set; } = null!;
        public string Prompt { get; set; } = null!;
        public double Temperature { get; set; } = 0.3;
        public double TopP { get; set; } = 0.9;
        public int MaxOutputTokens { get; set; } = 4096;
    }
}
