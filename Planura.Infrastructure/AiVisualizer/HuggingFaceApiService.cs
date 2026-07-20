using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Planura.Core.Application.Abstraction.AiVisualizer;
using Planura.Core.Application.Common;
using Planura.Shared.Errors.Models;

namespace Planura.Infrastructure.AiVisualizer
{
    // Talks to the Hugging Face Serverless Inference API. Configured for
    // stabilityai/stable-diffusion-3-medium-diffusers on the hf-inference
    // provider - a text-to-image model, not image-to-image - because every
    // third-party provider (fal-ai/replicate/wavespeed, which do host the
    // originally-intended FLUX.1-Kontext-dev image-editing model) rejects
    // this account regardless of model or gating status, while hf-inference
    // works. hf-inference itself has no image-to-image models at all, so
    // request.ImageBytes/ImageContentType are intentionally unused below -
    // the uploaded photo can't guide the output on this provider. Request:
    // { "inputs": "<prompt>" } in, raw image bytes back on success, a JSON
    // { "error": "..." } body on failure (503 while the model is
    // cold-starting, 429 when rate limited). Swap Model/BaseUrl back to
    // FLUX.1-Kontext-dev + a third-party provider (see HuggingFaceOptions)
    // to restore true image-to-image, no other code changes needed - this
    // method only builds request.Prompt into the wire payload.
    public class HuggingFaceApiService : IHuggingFaceApiService
    {
        private readonly HuggingFaceOptions _options;
        private readonly HttpClient _httpClient;

        public HuggingFaceApiService(IOptions<HuggingFaceOptions> options, HttpClient httpClient)
        {
            _options = options.Value;
            _httpClient = httpClient;
        }

        public async Task<GeneratedImageResult> GenerateImageAsync(HuggingFaceImageRequest request, CancellationToken cancellationToken = default)
        {
            var payload = new HuggingFaceTextToImageRequest(request.Prompt);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildRequestUri())
            {
                Content = JsonContent.Create(payload)
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            // Deliberately no Accept header. HF's router does a naive
            // exact-string check against it rather than real content
            // negotiation: a wildcard ("image/*") is rejected outright, and
            // even a valid comma-separated list ("image/png, image/jpeg")
            // is rejected as a single unrecognized value. It already
            // returns raw image bytes by default with no Accept header set
            // at all (confirmed via live calls), so omit it entirely.

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new AiProviderTimeoutExeption("The image generation model took too long to respond. Please try again.");
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new AiProviderRateLimitedExeption("The image generation service is temporarily busy. Please try again shortly.");
            }

            var responseContentType = response.Content.Headers.ContentType?.MediaType;
            var isImageResponse = response.IsSuccessStatusCode
                && responseContentType is not null
                && responseContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

            if (!isImageResponse)
            {
                var errorMessage = await ReadErrorMessageAsync(response, cancellationToken);

                if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    throw new AiProviderUnavailableExeption($"The image generation model is warming up, please retry shortly: {errorMessage}");
                }

                if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
                {
                    throw new AiContentPolicyExeption($"The image could not be generated: {errorMessage}");
                }

                throw new AiProviderUnavailableExeption($"The image generation service is currently unavailable: {errorMessage}");
            }

            var imageBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (imageBytes.Length == 0)
            {
                throw new AiProviderUnavailableExeption("The image generation service returned an empty image.");
            }

            return new GeneratedImageResult
            {
                ImageBytes = imageBytes,
                ContentType = responseContentType!
            };
        }

        private Uri BuildRequestUri() => new($"{_options.BaseUrl.TrimEnd('/')}/{_options.Model}");

        private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            try
            {
                var errorBody = await response.Content.ReadFromJsonAsync<HuggingFaceErrorResponse>(cancellationToken: cancellationToken);
                return errorBody?.Error ?? $"HTTP {(int)response.StatusCode}";
            }
            catch
            {
                return $"HTTP {(int)response.StatusCode}";
            }
        }

        private record HuggingFaceTextToImageRequest(string Inputs);

        private record HuggingFaceErrorResponse(
            [property: JsonPropertyName("error")] string? Error,
            [property: JsonPropertyName("estimated_time")] double? EstimatedTime);
    }
}
