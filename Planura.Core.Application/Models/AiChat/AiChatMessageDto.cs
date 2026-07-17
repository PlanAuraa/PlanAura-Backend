namespace Planura.Core.Application.Models;

public class AiChatMessageDto
{
    public long Id { get; set; }
    public string Role { get; set; } = null!;
    public string Content { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}
