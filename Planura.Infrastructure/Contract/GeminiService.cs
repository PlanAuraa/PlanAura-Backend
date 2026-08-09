using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Planura.Core.Application.Abstraction.Contract;
using Planura.Core.Application.Common;
using Planura.Shared.Errors.Models;

namespace Planura.Infrastructure.Contract
{
    // Talks directly to Google's Gemini REST API (v1beta generateContent) using a plain
    // HttpClient - no unofficial SDK. Deliberately generic: it turns a system instruction + user
    // prompt into text. All prompt engineering (what kind of contract, which facts, which legal
    // clauses) lives in Core.Application services - this class only knows how to reach Gemini.
    public class GeminiService : IGeminiService
    {
        private readonly GeminiOptions _options;
        private readonly HttpClient _httpClient;

        public GeminiService(IOptions<GeminiOptions> options, HttpClient httpClient)
        {
            _options = options.Value;
            _httpClient = httpClient;
        }

        public async Task<string> GenerateTextAsync(GeminiTextGenerationRequest request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                throw new AiProviderUnavailableExeption(
                    "Document generation is not configured yet. Ask an administrator to set the Gemini API key.");
            }

            var requestUrl = $"{_options.BaseUrl.TrimEnd('/')}/{_options.Model}:generateContent";

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = JsonContent.Create(BuildGeminiRequest(request))
            };
            httpRequest.Headers.Add("x-goog-api-key", _options.ApiKey);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new AiProviderTimeoutExeption("The AI assistant took too long to draft the document. Please try again.");
            }
            catch (HttpRequestException ex)
            {
                throw new AiProviderUnavailableExeption($"Could not reach the AI assistant: {ex.Message}");
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new AiProviderRateLimitedExeption("The AI assistant is temporarily busy. Please try again shortly.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await TryReadErrorMessageAsync(response, cancellationToken);
                throw new AiProviderUnavailableExeption($"The AI assistant could not draft the document: {errorMessage}");
            }

            GeminiResponse? result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: cancellationToken);
            }
            catch (JsonException)
            {
                throw new AiProviderUnavailableExeption("The AI assistant returned an unreadable response. Please try again.");
            }

            if (result?.PromptFeedback?.BlockReason is { Length: > 0 } blockReason)
            {
                throw new AiContentPolicyExeption(
                    $"The document could not be generated because the request was blocked by the AI's content policy ({blockReason}).");
            }

            var text = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Text))?.Text;

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new AiProviderUnavailableExeption("The AI assistant returned an empty document. Please try again.");
            }

            return CleanText(text);
        }

        private static async Task<string> TryReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            try
            {
                var errorBody = await response.Content.ReadFromJsonAsync<GeminiErrorResponse>(cancellationToken: cancellationToken);
                if (!string.IsNullOrWhiteSpace(errorBody?.Error?.Message))
                {
                    return errorBody!.Error!.Message!;
                }
            }
            catch (JsonException)
            {
                // fall through to the generic status-code message below
            }

            return $"HTTP {(int)response.StatusCode} ({response.StatusCode})";
        }

        // Strips accidental markdown code fences in case the model ignores the "no markdown" instruction.
        private static string CleanText(string text)
        {
            var trimmed = text.Trim();

            if (trimmed.StartsWith("```"))
            {
                var firstNewLine = trimmed.IndexOf('\n');
                if (firstNewLine >= 0)
                {
                    trimmed = trimmed[(firstNewLine + 1)..];
                }

                var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
                if (lastFence >= 0)
                {
                    trimmed = trimmed[..lastFence];
                }
            }

            return trimmed.Trim();
        }

        private static GeminiRequest BuildGeminiRequest(GeminiTextGenerationRequest request)
        {
            return new GeminiRequest(
                SystemInstruction: new GeminiContent("model", new[] { new GeminiPart(request.SystemInstruction) }),
                Contents: new[] { new GeminiContent("user", new[] { new GeminiPart(request.Prompt) }) },
                GenerationConfig: new GeminiGenerationConfig
                {
                    Temperature = request.Temperature,
                    TopP = request.TopP,
                    MaxOutputTokens = request.MaxOutputTokens,
                    ResponseMimeType = request.ResponseMimeType,
                    // Only meaningful alongside a JSON mime type; sending it otherwise is rejected.
                    ResponseSchema = string.IsNullOrWhiteSpace(request.ResponseMimeType) ? null : request.ResponseSchema
                });
        }

        // ---- Gemini v1beta generateContent request/response DTOs ----

        private record GeminiRequest(
            [property: JsonPropertyName("systemInstruction")] GeminiContent SystemInstruction,
            [property: JsonPropertyName("contents")] IReadOnlyList<GeminiContent> Contents,
            [property: JsonPropertyName("generationConfig")] GeminiGenerationConfig GenerationConfig);

        private record GeminiContent(
            [property: JsonPropertyName("role")] string Role,
            [property: JsonPropertyName("parts")] IReadOnlyList<GeminiPart> Parts);

        private record GeminiPart([property: JsonPropertyName("text")] string Text);

        private sealed class GeminiGenerationConfig
        {
            [JsonPropertyName("temperature")]
            public double Temperature { get; init; }

            [JsonPropertyName("topP")]
            public double TopP { get; init; }

            [JsonPropertyName("maxOutputTokens")]
            public int MaxOutputTokens { get; init; }

            [JsonPropertyName("responseMimeType")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? ResponseMimeType { get; init; }

            [JsonPropertyName("responseSchema")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public object? ResponseSchema { get; init; }
        }

        private record GeminiResponse(
            [property: JsonPropertyName("candidates")] IReadOnlyList<GeminiCandidate>? Candidates,
            [property: JsonPropertyName("promptFeedback")] GeminiPromptFeedback? PromptFeedback);

        private record GeminiCandidate([property: JsonPropertyName("content")] GeminiContent? Content);

        private record GeminiPromptFeedback([property: JsonPropertyName("blockReason")] string? BlockReason);

        private record GeminiErrorResponse([property: JsonPropertyName("error")] GeminiErrorDetail? Error);

        private record GeminiErrorDetail(
            [property: JsonPropertyName("code")] int? Code,
            [property: JsonPropertyName("message")] string? Message,
            [property: JsonPropertyName("status")] string? Status);
    }
}
