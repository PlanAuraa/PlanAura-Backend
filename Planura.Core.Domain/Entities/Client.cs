namespace Planura.Core.Domain.Entities;

public class Client
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string? City { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? AvatarUrl { get; set; }

    // Deposit / partial-payment (Phase 2). The client's Stripe Customer, created lazily on their first
    // deposit-path booking so a saved card can be charged off-session for the remainder later. Reused
    // across bookings once set. Null until the client makes a deposit booking.
    public string? StripeCustomerId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ApplicationUser User { get; set; } = null!;
    public ICollection<EventPlan> EventPlans { get; set; } = new List<EventPlan>();
    public ICollection<BookingRequest> BookingRequests { get; set; } = new List<BookingRequest>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<AiChatConversation> AiChatConversations { get; set; } = new List<AiChatConversation>();
}
