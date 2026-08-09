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

        /// <summary>
        /// Response MIME type to request, e.g. "application/json" for structured output. Null leaves
        /// the model in its default free-text mode.
        /// </summary>
        public string? ResponseMimeType { get; set; }

        /// <summary>
        /// OpenAPI-subset schema the response must conform to (Gemini's <c>responseSchema</c>). Only
        /// honoured alongside a JSON <see cref="ResponseMimeType"/>. Supplying this makes the model's
        /// output parseable and validatable rather than free prose we have to regex apart.
        /// </summary>
        public object? ResponseSchema { get; set; }
    }
}
