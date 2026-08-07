namespace Planura.Core.Application.Models;

/// <summary>A service category the client marked as needed for this plan. IsSatisfied is computed
/// (not stored) — true when the plan has an active booking whose vendor belongs to this category.</summary>
public class EventPlanChecklistItemDto
{
    public long ServiceCategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public string? IconUrl { get; set; }
    public bool IsSatisfied { get; set; }
}
