namespace Planura.Core.Domain.Entities;

public class BookingChatMessage
{
    public long Id { get; set; }
    public long BookingRequestId { get; set; }
    public long SenderUserId { get; set; }
    public string Content { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public BookingRequest BookingRequest { get; set; } = null!;
    public ApplicationUser SenderUser { get; set; } = null!;
}
