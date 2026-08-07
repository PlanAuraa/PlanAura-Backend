namespace Planura.Core.Domain.Entities;

/// <summary>
/// A service category the client has marked as needed for this event plan (e.g. Venue,
/// Photographer, Catering). "Satisfied" is not stored here — it's computed by checking whether
/// the plan has a booking whose vendor belongs to this category (see EventPlanService).
/// </summary>
public class EventPlanChecklistItem
{
    public long Id { get; set; }
    public long EventPlanId { get; set; }
    public long ServiceCategoryId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public EventPlan EventPlan { get; set; } = null!;
    public ServiceCategory ServiceCategory { get; set; } = null!;
}
