namespace Planura.Core.Domain.Entities;

public class EventPlan
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    public string? Title { get; set; }
    public string EventType { get; set; } = null!;
    public DateOnly? EventDate { get; set; }
    public string? City { get; set; }
    public int? GuestCount { get; set; }
    public decimal? BudgetTotal { get; set; }
    public string? StyleNotes { get; set; }
    public string Status { get; set; } = "draft";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;


    //test

    public Client Client { get; set; } = null!;
    public ICollection<EventPlanItem> Items { get; set; } = new List<EventPlanItem>();
    public ICollection<BookingRequest> BookingRequests { get; set; } = new List<BookingRequest>();
    public ICollection<AiEventVisualization> AiEventVisualizations { get; set; } = new List<AiEventVisualization>();
    public ICollection<AiInvitation> AiInvitations { get; set; } = new List<AiInvitation>();
}
