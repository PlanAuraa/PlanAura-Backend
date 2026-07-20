using Microsoft.Extensions.Logging;
using Moq;
using Planura.Core.Application.Abstraction.AiVisualizer;
using Planura.Core.Application.Abstraction.AttachementService;
using Planura.Core.Application.Models.AiVisualizer;
using Planura.Core.Application.Services.AiVisualizer;
using Planura.Shared.Errors.Models;
using Planura.Tests.TestHelpers;
using Xunit;

namespace Planura.Tests.Services;

/// <summary>
/// Unit tests for the AiVisualizerService application-layer service - the
/// "handler" equivalent for the visualize-event use case in this codebase's
/// plain service-layer architecture (there is no MediatR/CQRS pipeline here;
/// see AiChatService for the same pattern used elsewhere). Verifies the
/// business logic in isolation: input validation, that IHuggingFaceApiService
/// is invoked with the right payload, and that both the original and
/// generated images are persisted via IAttachmentService before the response
/// is built.
/// </summary>
public class AiVisualizerServiceTests
{
    private readonly Mock<IAttachmentService> _attachmentServiceMock = new();
    private readonly Mock<IHuggingFaceApiService> _huggingFaceApiServiceMock = new();
    private readonly Mock<ILogger<AiVisualizerService>> _loggerMock = new();

    private AiVisualizerService CreateService() => new(
        _attachmentServiceMock.Object,
        _huggingFaceApiServiceMock.Object,
        _loggerMock.Object);

    [Fact]
    public async Task VisualizeEventAsync_MissingImage_ThrowsBadRequestExeptionWithoutCallingProvider()
    {
        var dto = new VisualizeEventDto
        {
            Image = FormFileFactory.Create(sizeBytes: 0),
            Prompt = "A modern wedding setup with white roses."
        };

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.VisualizeEventAsync(clientUserId: 1, dto));

        _huggingFaceApiServiceMock.Verify(
            p => p.GenerateImageAsync(It.IsAny<HuggingFaceImageRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task VisualizeEventAsync_BlankPrompt_ThrowsBadRequestExeption(string prompt)
    {
        var dto = new VisualizeEventDto
        {
            Image = FormFileFactory.Create(),
            Prompt = prompt
        };

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.VisualizeEventAsync(clientUserId: 1, dto));
    }

    [Fact]
    public async Task VisualizeEventAsync_PromptTooLong_ThrowsBadRequestExeption()
    {
        var dto = new VisualizeEventDto
        {
            Image = FormFileFactory.Create(),
            Prompt = new string('a', 501)
        };

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.VisualizeEventAsync(clientUserId: 1, dto));
    }

    [Fact]
    public async Task VisualizeEventAsync_Valid_UploadsOriginalCallsProviderAndStoresGeneratedImage()
    {
        var dto = new VisualizeEventDto
        {
            Image = FormFileFactory.Create(fileName: "hall.jpg", contentType: "image/jpeg"),
            Prompt = "  A modern wedding setup with white roses, fairy lights, and a luxury stage.  "
        };

        _attachmentServiceMock
            .Setup(a => a.UploadAsynce(dto.Image, "ai-visualizer/originals"))
            .ReturnsAsync("images/ai-visualizer/originals/original.jpg");

        HuggingFaceImageRequest? capturedRequest = null;
        _huggingFaceApiServiceMock
            .Setup(p => p.GenerateImageAsync(It.IsAny<HuggingFaceImageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<HuggingFaceImageRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new GeneratedImageResult
            {
                ImageBytes = new byte[] { 9, 9, 9 },
                ContentType = "image/png"
            });

        _attachmentServiceMock
            .Setup(a => a.UploadGeneratedFileAsync(It.IsAny<byte[]>(), ".png", "ai-visualizer/generated"))
            .ReturnsAsync("images/ai-visualizer/generated/generated.png");

        _attachmentServiceMock
            .Setup(a => a.ToAbsoluteUrl("images/ai-visualizer/originals/original.jpg"))
            .Returns("https://api.planura.local/images/ai-visualizer/originals/original.jpg");
        _attachmentServiceMock
            .Setup(a => a.ToAbsoluteUrl("images/ai-visualizer/generated/generated.png"))
            .Returns("https://api.planura.local/images/ai-visualizer/generated/generated.png");

        var service = CreateService();
        var result = await service.VisualizeEventAsync(clientUserId: 42, dto);

        Assert.Equal("https://api.planura.local/images/ai-visualizer/originals/original.jpg", result.OriginalImageUrl);
        Assert.Equal("https://api.planura.local/images/ai-visualizer/generated/generated.png", result.GeneratedImageUrl);
        Assert.Equal("A modern wedding setup with white roses, fairy lights, and a luxury stage.", result.Prompt);

        Assert.NotNull(capturedRequest);
        Assert.Equal("image/jpeg", capturedRequest!.ImageContentType);
        Assert.Equal("A modern wedding setup with white roses, fairy lights, and a luxury stage.", capturedRequest.Prompt);
        Assert.NotEmpty(capturedRequest.ImageBytes);

        _attachmentServiceMock.Verify(a => a.UploadAsynce(dto.Image, "ai-visualizer/originals"), Times.Once);
        _attachmentServiceMock.Verify(
            a => a.UploadGeneratedFileAsync(It.IsAny<byte[]>(), ".png", "ai-visualizer/generated"),
            Times.Once);
    }

    [Fact]
    public async Task VisualizeEventAsync_ProviderThrowsAiException_PropagatesWithoutStoringGeneratedFile()
    {
        var dto = new VisualizeEventDto
        {
            Image = FormFileFactory.Create(),
            Prompt = "A modern wedding setup."
        };

        _attachmentServiceMock
            .Setup(a => a.UploadAsynce(dto.Image, "ai-visualizer/originals"))
            .ReturnsAsync("images/ai-visualizer/originals/original.jpg");

        _huggingFaceApiServiceMock
            .Setup(p => p.GenerateImageAsync(It.IsAny<HuggingFaceImageRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AiProviderUnavailableExeption("The image generation service is currently unavailable."));

        var service = CreateService();

        await Assert.ThrowsAsync<AiProviderUnavailableExeption>(() => service.VisualizeEventAsync(clientUserId: 1, dto));

        _attachmentServiceMock.Verify(
            a => a.UploadGeneratedFileAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }
}
