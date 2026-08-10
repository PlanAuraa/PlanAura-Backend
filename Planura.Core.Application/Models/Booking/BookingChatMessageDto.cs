namespace Planura.Core.Application.Models;

public class BookingChatMessageDto
{
    public long Id { get; set; }
    public long BookingRequestId { get; set; }
    public long SenderUserId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public bool IsMine { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
