namespace Planura.Core.Domain.Entities;

public class Notification
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string? Type { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? DataJson { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ApplicationUser User { get; set; } = null!;
}
