namespace Planura.Core.Application.Models;

/// <summary>
/// Generates individual VendorAvailability slots from a weekly pattern (e.g. "every Friday and
/// Saturday, 14:00-22:00, for the next 3 months") instead of the vendor creating each slot by hand.
/// StartTime/EndTime are interpreted as UTC, matching how single-slot StartAt/EndAt values already
/// arrive from the client (ISO strings normalized to UTC).
/// </summary>
public class CreateRecurringAvailabilityDto
{
    /// <summary>0=Sunday .. 6=Saturday (matches System.DayOfWeek's numbering).</summary>
    public int[] DaysOfWeek { get; set; } = [];

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public DateOnly StartDate { get; set; }

    /// <summary>How many months from StartDate to generate slots for.</summary>
    public int RepeatMonths { get; set; }
}
