using Planura.Core.Application.Models.AiVisualizer;

namespace Planura.Core.Application.Services.AiVisualizer;

public interface IAiVisualizerService
{
    Task<VisualizeEventResponseDto> VisualizeEventAsync(long clientUserId, VisualizeEventDto dto);
}
