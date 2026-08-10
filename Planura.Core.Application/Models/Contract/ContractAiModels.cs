namespace Planura.Core.Application.Models;

/// <summary>
/// The Event Booking Contract as structured data rather than free prose - built deterministically by
/// <c>ContractService.BuildTemplateDraft</c> from a <see cref="ContractGenerationContext"/>. Structure
/// is what lets the PDF render real sections and lists (see <c>PdfService</c>) instead of parsing prose.
/// </summary>
public sealed class ContractDraft
{
    public string? Title { get; set; }

    public string? Preamble { get; set; }

    public List<ContractDraftSection> Sections { get; set; } = new();
}

public sealed class ContractDraftSection
{
    public string? Title { get; set; }

    public List<string> Paragraphs { get; set; } = new();

    /// <summary>Enumerated points within the section, rendered as (a), (b), (c)… in the PDF.</summary>
    public List<string> Items { get; set; } = new();
}
