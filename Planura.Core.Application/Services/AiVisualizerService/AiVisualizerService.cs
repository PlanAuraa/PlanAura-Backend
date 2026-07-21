using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Planura.Core.Application.Abstraction.AiVisualizer;
using Planura.Core.Application.Abstraction.AttachementService;
using Planura.Core.Application.Models.AiVisualizer;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services.AiVisualizer;

public class AiVisualizerService : IAiVisualizerService
{
    private const int MaxPromptLength = 500;

    private readonly IAttachmentService _attachmentService;
    private readonly IHuggingFaceApiService _huggingFaceApiService;
    private readonly ILogger<AiVisualizerService> _logger;

    public AiVisualizerService(
        IAttachmentService attachmentService,
        IHuggingFaceApiService huggingFaceApiService,
        ILogger<AiVisualizerService> logger)
    {
        _attachmentService = attachmentService;
        _huggingFaceApiService = huggingFaceApiService;
        _logger = logger;
    }

    public async Task<VisualizeEventResponseDto> VisualizeEventAsync(long clientUserId, VisualizeEventDto dto)
    {
        ValidateRequest(dto);

        var prompt = dto.Prompt.Trim();
        var imageBytes = await ReadAllBytesAsync(dto.Image);

        // Store the client's original upload first so we still have a
        // record of it (and something to show side-by-side) even if the
        // provider call below fails.
        var originalImagePath = await _attachmentService.UploadAsynce(dto.Image, "ai-visualizer/originals")
            ?? throw new BadRequestExeption("Unable to store the uploaded image.");

        _logger.LogInformation(
            "Requesting AI event visualization for client user {ClientUserId} with prompt length {PromptLength}.",
            clientUserId, prompt.Length);

        var generated = await _huggingFaceApiService.GenerateImageAsync(new HuggingFaceImageRequest
        {
            ImageBytes = imageBytes,
            ImageContentType = dto.Image.ContentType,
            Prompt = prompt
        });

        var generatedImagePath = await _attachmentService.UploadGeneratedFileAsync(
            generated.ImageBytes,
            ExtensionFromContentType(generated.ContentType),
            "ai-visualizer/generated")
            ?? throw new AiProviderUnavailableExeption("The generated image could not be stored.");

        return new VisualizeEventResponseDto
        {
            OriginalImageUrl = _attachmentService.ToAbsoluteUrl(originalImagePath) ?? originalImagePath,
            GeneratedImageUrl = _attachmentService.ToAbsoluteUrl(generatedImagePath) ?? generatedImagePath,
            Prompt = prompt
        };
    }

    private static void ValidateRequest(VisualizeEventDto dto)
    {
        if (dto.Image is null || dto.Image.Length == 0)
        {
            throw new BadRequestExeption("An image of the space is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Prompt))
        {
            throw new BadRequestExeption("A description of the desired event setup is required.");
        }

        if (dto.Prompt.Trim().Length > MaxPromptLength)
        {
            throw new BadRequestExeption($"The prompt must be {MaxPromptLength} characters or fewer.");
        }
    }

    private static async Task<byte[]> ReadAllBytesAsync(IFormFile file)
    {
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }

    private static string ExtensionFromContentType(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".jpg"
    };
}
