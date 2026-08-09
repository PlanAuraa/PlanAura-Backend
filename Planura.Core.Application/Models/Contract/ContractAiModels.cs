using System.Text.Json.Serialization;

namespace Planura.Core.Application.Models;

/// <summary>
/// Stage 1 of contract generation: the model's reading of what the parties actually agreed, derived
/// strictly from the <see cref="ContractGenerationContext"/> fact sheet. Extracting the deal before
/// drafting is what makes the final document argue from this transaction rather than reach for a
/// familiar template — and it gives us an inspectable, loggable intermediate when a contract looks wrong.
/// </summary>
public sealed class ContractDealAnalysis
{
    [JsonPropertyName("serviceSummary")]
    public string? ServiceSummary { get; set; }

    [JsonPropertyName("serviceCategory")]
    public string? ServiceCategory { get; set; }

    [JsonPropertyName("scopeItems")]
    public List<string> ScopeItems { get; set; } = new();

    [JsonPropertyName("deliverables")]
    public List<string> Deliverables { get; set; } = new();

    [JsonPropertyName("clientRequirements")]
    public List<string> ClientRequirements { get; set; } = new();

    [JsonPropertyName("vendorCommitments")]
    public List<string> VendorCommitments { get; set; } = new();

    [JsonPropertyName("clientObligations")]
    public List<string> ClientObligations { get; set; } = new();

    [JsonPropertyName("timeline")]
    public List<ContractTimelineEntry> Timeline { get; set; } = new();

    [JsonPropertyName("financialTerms")]
    public List<string> FinancialTerms { get; set; } = new();

    [JsonPropertyName("specialConditions")]
    public List<string> SpecialConditions { get; set; } = new();

    [JsonPropertyName("cancellationAndRisk")]
    public List<string> CancellationAndRisk { get; set; } = new();

    /// <summary>Contractually material points the fact sheet does not settle. Never guessed at.</summary>
    [JsonPropertyName("openPoints")]
    public List<string> OpenPoints { get; set; } = new();

    /// <summary>The clause set this particular transaction warrants, chosen by the model.</summary>
    [JsonPropertyName("requiredClauses")]
    public List<ContractClausePlan> RequiredClauses { get; set; } = new();
}

public sealed class ContractTimelineEntry
{
    [JsonPropertyName("milestone")]
    public string? Milestone { get; set; }

    [JsonPropertyName("when")]
    public string? When { get; set; }
}

public sealed class ContractClausePlan
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

/// <summary>
/// Stage 2: the finished contract as structured data rather than free prose. Structure is what lets
/// the PDF render real sections and lists, and lets <c>ContractFactValidator</c> check the document
/// against the source facts before anyone signs it.
/// </summary>
public sealed class ContractDraft
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("preamble")]
    public string? Preamble { get; set; }

    [JsonPropertyName("sections")]
    public List<ContractDraftSection> Sections { get; set; } = new();
}

public sealed class ContractDraftSection
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("paragraphs")]
    public List<string> Paragraphs { get; set; } = new();

    /// <summary>Enumerated points within the section, rendered as (a), (b), (c)… in the PDF.</summary>
    [JsonPropertyName("items")]
    public List<string> Items { get; set; } = new();
}
