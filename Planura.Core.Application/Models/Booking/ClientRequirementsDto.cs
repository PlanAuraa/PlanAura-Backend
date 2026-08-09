using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models;

/// <summary>
/// What this client specifically wants from this booking, captured at the booking step and carried
/// into the Booking Agreement so the contract states the client's own requirements rather than
/// describing the service in general terms.
/// <para>
/// Every field is optional. A requirement the client did not state is omitted from the contract
/// entirely - it is never filled in with a plausible default.
/// </para>
/// </summary>
public class ClientRequirementsDto
{
    /// <summary>What the client expects to receive, in their own words (e.g. "500 edited photos").</summary>
    [StringLength(1000)]
    public string? Deliverables { get; set; }

    /// <summary>Style, theme or presentation preferences for this booking.</summary>
    [StringLength(500)]
    public string? StylePreferences { get; set; }

    /// <summary>Timing requirements beyond the booked slot (e.g. setup time, delivery deadline).</summary>
    [StringLength(500)]
    public string? TimingRequirements { get; set; }

    /// <summary>Where the service is to be performed, when more specific than the event plan's city.</summary>
    [StringLength(300)]
    public string? LocationDetails { get; set; }

    /// <summary>Anything else the client needs the vendor to accommodate.</summary>
    [StringLength(1000)]
    public string? SpecialRequests { get; set; }

    /// <summary>
    /// Flattens the stated requirements into labelled lines for the contract context. Empty fields
    /// produce no line at all, so an unanswered question never becomes a contract term.
    /// </summary>
    public IReadOnlyList<string> ToRequirementLines()
    {
        var lines = new List<string>();

        Add("Deliverables requested by the client", Deliverables);
        Add("Style and presentation preferences", StylePreferences);
        Add("Timing requirements", TimingRequirements);
        Add("Location details supplied by the client", LocationDetails);
        Add("Special requests", SpecialRequests);

        return lines;

        void Add(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                lines.Add($"{label}: {value.Trim()}");
            }
        }
    }
}
