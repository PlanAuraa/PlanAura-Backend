namespace Planura.Core.Application.Models.AiVisualizer;

public class VisualizeEventResponseDto
{
    public string OriginalImageUrl { get; set; } = string.Empty;
    public string GeneratedImageUrl { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
}
