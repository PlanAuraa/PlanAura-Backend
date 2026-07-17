namespace Planura.Core.Application.Common;

public class OpenAiOptions
{
    public const string SectionName = "OpenAi";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";

    // Base URL for the chat gateway. When set, the provider posts to this
    // endpoint directly instead of the official OpenAI SDK's default host -
    // used to route through the ITI student LLM gateway (a custom,
    // non-OpenAI-compatible proxy in front of AWS Bedrock).
    public string? BaseUrl { get; set; }
}
