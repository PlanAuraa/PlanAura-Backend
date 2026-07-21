namespace Planura.Core.Application.Common;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.5-flash";

    // Base URL for the Gemini "generateContent" REST endpoint, without the trailing
    // "/{model}:generateContent" suffix - GeminiService appends that itself.
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/models";
}
