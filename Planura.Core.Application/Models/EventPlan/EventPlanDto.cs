namespace Planura.Core.Application.Models;

public class EventPlanDto
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
    public string Status { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
