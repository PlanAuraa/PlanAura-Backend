namespace Planura.Core.Application.Models;

public class CreateEventPlanDto
{
    public string? Title { get; set; }
    public string EventType { get; set; } = null!;
    public DateOnly? EventDate { get; set; }
    public string? City { get; set; }
    public int? GuestCount { get; set; }
    public decimal? BudgetTotal { get; set; }
    public string? StyleNotes { get; set; }
}
