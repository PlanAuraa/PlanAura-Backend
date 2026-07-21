namespace Planura.Core.Application.Abstraction.AiVisualizer
{
    public interface IHuggingFaceApiService
    {
        Task<GeneratedImageResult> GenerateImageAsync(HuggingFaceImageRequest request, CancellationToken cancellationToken = default);
    }
}
