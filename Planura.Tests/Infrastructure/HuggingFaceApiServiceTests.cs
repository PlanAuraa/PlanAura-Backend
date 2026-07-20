using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Planura.Core.Application.Abstraction.AiVisualizer;
using Planura.Core.Application.Common;
using Planura.Infrastructure.AiVisualizer;
using Planura.Shared.Errors.Models;
using Xunit;

namespace Planura.Tests.Infrastructure;

/// <summary>
/// Exercises HuggingFaceApiService's HTTP behavior directly against a mocked
/// HttpMessageHandler (via Moq.Protected), since it talks to the Hugging
/// Face Inference API through a plain typed HttpClient rather than a
/// higher-level SDK. Mirrors the request/response contract described in
/// HuggingFaceApiService's header comment.
/// </summary>
public class HuggingFaceApiServiceTests
{
    private static readonly HuggingFaceOptions Options = new()
    {
        ApiKey = "hf_test_token",
        Model = "stabilityai/stable-diffusion-3-medium-diffusers",
        BaseUrl = "https://router.huggingface.co/hf-inference/models"
    };

    private static HuggingFaceImageRequest CreateRequest() => new()
    {
        ImageBytes = Encoding.UTF8.GetBytes("fake-source-image-bytes"),
        ImageContentType = "image/jpeg",
        Prompt = "A modern wedding setup with white roses, fairy lights, and a luxury stage."
    };

    private static (HuggingFaceApiService Service, Mock<HttpMessageHandler> HandlerMock) CreateService(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) => respond(req));

        var httpClient = new HttpClient(handlerMock.Object);
        var service = new HuggingFaceApiService(Microsoft.Extensions.Options.Options.Create(Options), httpClient);

        return (service, handlerMock);
    }

    private static (HuggingFaceApiService Service, Mock<HttpMessageHandler> HandlerMock) CreateThrowingService(Exception exception)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(exception);

        var httpClient = new HttpClient(handlerMock.Object);
        var service = new HuggingFaceApiService(Microsoft.Extensions.Options.Options.Create(Options), httpClient);

        return (service, handlerMock);
    }

    [Fact]
    public async Task GenerateImageAsync_Success_SendsBearerTokenAndPromptPayloadToCorrectUrl()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var responseBytes = new byte[] { 1, 2, 3, 4 };

        var (service, handlerMock) = CreateService(req =>
        {
            capturedRequest = req;
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(responseBytes)
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            return response;
        });

        var request = CreateRequest();
        var result = await service.GenerateImageAsync(request);

        Assert.Equal(responseBytes, result.ImageBytes);
        Assert.Equal("image/png", result.ContentType);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal(
            "https://router.huggingface.co/hf-inference/models/stabilityai/stable-diffusion-3-medium-diffusers",
            capturedRequest.RequestUri!.ToString());
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization!.Scheme);
        Assert.Equal("hf_test_token", capturedRequest.Headers.Authorization.Parameter);

        // hf-inference only exposes text-to-image here (no image-to-image
        // model is live on that provider), so the wire payload is just the
        // prompt - request.ImageBytes/ImageContentType are not sent.
        using var bodyJson = JsonDocument.Parse(capturedBody!);
        Assert.Equal(request.Prompt, bodyJson.RootElement.GetProperty("inputs").GetString());
        Assert.False(bodyJson.RootElement.TryGetProperty("parameters", out _));

        handlerMock.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GenerateImageAsync_TooManyRequests_ThrowsAiProviderRateLimitedExeption()
    {
        var (service, _) = CreateService(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = JsonContent.Create(new { error = "Rate limit reached" })
        });

        await Assert.ThrowsAsync<AiProviderRateLimitedExeption>(() => service.GenerateImageAsync(CreateRequest()));
    }

    [Fact]
    public async Task GenerateImageAsync_ServiceUnavailable_ThrowsAiProviderUnavailableExeptionWithModelLoadingMessage()
    {
        var (service, _) = CreateService(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = JsonContent.Create(new { error = "Model is currently loading", estimated_time = 20.0 })
        });

        var exception = await Assert.ThrowsAsync<AiProviderUnavailableExeption>(() => service.GenerateImageAsync(CreateRequest()));
        Assert.Contains("Model is currently loading", exception.Message);
    }

    [Fact]
    public async Task GenerateImageAsync_UnprocessableEntity_ThrowsAiContentPolicyExeption()
    {
        var (service, _) = CreateService(_ => new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
        {
            Content = JsonContent.Create(new { error = "Prompt violates content policy" })
        });

        var exception = await Assert.ThrowsAsync<AiContentPolicyExeption>(() => service.GenerateImageAsync(CreateRequest()));
        Assert.Contains("content policy", exception.Message);
    }

    [Fact]
    public async Task GenerateImageAsync_GenericServerError_ThrowsAiProviderUnavailableExeption()
    {
        var (service, _) = CreateService(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("not json", Encoding.UTF8, "text/plain")
        });

        await Assert.ThrowsAsync<AiProviderUnavailableExeption>(() => service.GenerateImageAsync(CreateRequest()));
    }

    [Fact]
    public async Task GenerateImageAsync_SuccessStatusButEmptyBody_ThrowsAiProviderUnavailableExeption()
    {
        var (service, _) = CreateService(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Array.Empty<byte>())
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            return response;
        });

        await Assert.ThrowsAsync<AiProviderUnavailableExeption>(() => service.GenerateImageAsync(CreateRequest()));
    }

    [Fact]
    public async Task GenerateImageAsync_RequestTimesOut_ThrowsAiProviderTimeoutExeption()
    {
        var (service, _) = CreateThrowingService(new TaskCanceledException());

        await Assert.ThrowsAsync<AiProviderTimeoutExeption>(() => service.GenerateImageAsync(CreateRequest()));
    }
}
