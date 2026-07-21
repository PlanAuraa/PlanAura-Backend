using Microsoft.AspNetCore.Http;

namespace Planura.Core.Application.Models.AiVisualizer;

// Bound from multipart/form-data on AiController.Visualize - a photo of the
// empty venue plus the client's freeform description of the desired setup.
public class VisualizeEventDto
{
    public IFormFile Image { get; set; } = null!;
    public string Prompt { get; set; } = string.Empty;
}
