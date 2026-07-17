using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Planura.Core.Application.Abstraction.AiChat;
using Planura.Core.Application.Common;
using Planura.Shared.Errors.Models;

namespace Planura.Infrastructure.AiChat
{
    // Talks to the ITI student LLM gateway (BaseUrl in OpenAiOptions) - a custom
    // proxy in front of AWS Bedrock, not an OpenAI-compatible endpoint. Request
    // is { model_id, messages }; response is { output_text, usage, ... } rather
    // than the OpenAI choices[].message shape, so this uses a plain HttpClient
    // instead of the OpenAI SDK's ChatClient.
    public class OpenAiChatProvider : IAiChatProvider
    {
        private readonly OpenAiOptions _options;
        private readonly HttpClient _httpClient;

        public OpenAiChatProvider(IOptions<OpenAiOptions> options, HttpClient httpClient)
        {
            _options = options.Value;
            _httpClient = httpClient;
        }

        public async Task<AiChatCompletionResult> GetChatCompletionAsync(AiChatCompletionRequest request)
        {
            var messages = new List<GatewayMessage>
            {
                // The gateway's underlying models are inconsistent about supporting
                // a separate "system" role (some reject it outright), so the system
                // prompt is folded into a leading user/assistant exchange instead.
                new("user", request.SystemPrompt),
                new("assistant", "Understood, I will follow these instructions.")
            };

            foreach (var turn in request.History)
            {
                messages.Add(new GatewayMessage(turn.Role == "assistant" ? "assistant" : "user", turn.Content));
            }

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl)
            {
                Content = JsonContent.Create(new GatewayRequest(_options.Model, messages))
            };
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(httpRequest);
            }
            catch (TaskCanceledException)
            {
                throw new AiProviderTimeoutExeption("The AI assistant took too long to respond. Please try again.");
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new AiProviderRateLimitedExeption("The AI assistant is temporarily busy. Please try again shortly.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadFromJsonAsync<GatewayErrorResponse>();
                var errorMessage = errorBody?.Error?.Message ?? $"HTTP {(int)response.StatusCode}";
                throw new AiProviderUnavailableExeption($"The AI assistant is currently unavailable: {errorMessage}");
            }

            var result = await response.Content.ReadFromJsonAsync<GatewayResponse>()
                ?? throw new AiProviderUnavailableExeption("The AI assistant returned an empty response.");

            return new AiChatCompletionResult
            {
                Content = result.OutputText,
                ModelUsed = result.ModelId ?? _options.Model
            };
        }

        private record GatewayRequest([property: JsonPropertyName("model_id")] string ModelId, IReadOnlyList<GatewayMessage> Messages);

        private record GatewayMessage(string Role, string Content);

        private record GatewayResponse(
            [property: JsonPropertyName("output_text")] string OutputText,
            [property: JsonPropertyName("model_id")] string? ModelId);

        private record GatewayErrorResponse(GatewayErrorDetail? Error);

        private record GatewayErrorDetail(string? Code, string? Message);
    }
}
