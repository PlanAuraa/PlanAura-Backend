using System.Globalization;
using System.Text;
using Planura.Core.Application.Models;

namespace Planura.Core.Application.Services.Contract;

/// <summary>
/// Checks a generated draft against the facts it was supposed to be built from. This is the guard
/// against the two failure modes that matter most: a contract that silently omits a material term the
/// parties are relying on, and a contract that contradicts one.
/// <para>
/// Facts are graded. <see cref="ContractFactCheck.IsCritical"/> facts identify the deal itself - who
/// contracted with whom, for how much - and a draft missing one is not usable, so the caller
/// regenerates and ultimately refuses rather than shipping it. Everything else is important but
/// recoverable: it triggers one targeted regeneration, and if it still does not appear, it is logged
/// for follow-up rather than blocking a client mid-booking.
/// </para>
/// </summary>
internal static class ContractFactValidator
{
    public static ContractValidationResult Validate(ContractGenerationContext context, ContractDraft draft)
    {
        var haystack = Normalize(Flatten(draft));
        var checks = BuildChecks(context);

        var missing = checks
            .Where(check => !check.Matchers.Any(m => haystack.Contains(Normalize(m), StringComparison.Ordinal)))
            .ToList();

        return new ContractValidationResult(
            MissingCritical: missing.Where(m => m.IsCritical).Select(m => m.Description).ToList(),
            MissingImportant: missing.Where(m => !m.IsCritical).Select(m => m.Description).ToList());
    }

    private static List<ContractFactCheck> BuildChecks(ContractGenerationContext context)
    {
        var checks = new List<ContractFactCheck>();

        // ---- Identity of the deal. A draft missing any of these is not this booking's contract.
        AddText(checks, "Client legal name", context.Client.LegalName, critical: true);
        AddText(checks, "Vendor business name", context.Vendor.LegalName, critical: true);
        AddMoney(checks, "Total agreed amount", context.Financials.TotalAmount, context.Currency, critical: true);

        // ---- Material terms. Recoverable, but the contract is materially weaker without them.
        AddText(checks, "Selected package title", context.Service.PackageTitle);
        AddText(checks, "Service category", context.Service.Category);
        AddText(checks, "Event type", context.Booking.EventType);

        if (context.Booking.EventDate is { } eventDate)
        {
            checks.Add(new ContractFactCheck(
                $"Event date ({eventDate:MMMM d, yyyy})",
                new[]
                {
                    eventDate.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture),
                    eventDate.ToString("MMMM d yyyy", CultureInfo.InvariantCulture),
                    eventDate.ToString("d MMMM yyyy", CultureInfo.InvariantCulture),
                    eventDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                },
                IsCritical: false));
        }

        AddText(checks, "Service location", context.Booking.LocationDetail ?? context.Booking.City);

        if (context.Booking.GuestCount is { } guests)
        {
            checks.Add(new ContractFactCheck(
                $"Guest count ({guests})",
                new[] { guests.ToString(CultureInfo.InvariantCulture) },
                IsCritical: false));
        }

        if (context.Booking.DurationHours is { } hours)
        {
            var whole = (int)Math.Round(hours, MidpointRounding.AwayFromZero);
            checks.Add(new ContractFactCheck(
                $"Booked duration ({hours.ToString("0.##", CultureInfo.InvariantCulture)} hours)",
                new[]
                {
                    hours.ToString("0.##", CultureInfo.InvariantCulture),
                    whole.ToString(CultureInfo.InvariantCulture)
                },
                IsCritical: false));
        }

        if (context.Financials.IsDepositSchedule == true)
        {
            AddMoney(checks, "Deposit amount", context.Financials.DepositAmount, context.Currency);
            AddMoney(checks, "Remaining balance", context.Financials.RemainderAmount, context.Currency);
        }

        // ---- Cancellation schedule. The single term most likely to be quietly replaced with
        // industry-standard boilerplate that contradicts what the platform actually enforces.
        foreach (var tier in context.Policies.CancellationTiers.Where(t => t.MinDaysBefore > 0))
        {
            checks.Add(new ContractFactCheck(
                $"Cancellation tier ({tier.MinDaysBefore} days / {tier.RefundPercent:0.##}%)",
                new[] { $"{tier.RefundPercent.ToString("0.##", CultureInfo.InvariantCulture)}%" },
                IsCritical: false));
        }

        // ---- Deliverables the vendor committed to in their own words.
        foreach (var inclusion in context.Service.InclusionItems.Take(6))
        {
            var keyword = LongestWord(inclusion);
            if (keyword is not null)
            {
                checks.Add(new ContractFactCheck(
                    $"Package inclusion: {Truncate(inclusion, 70)}",
                    new[] { keyword },
                    IsCritical: false));
            }
        }

        // ---- Requirements this client actually stated. Their absence is exactly the "generic
        // template" failure this redesign exists to prevent.
        foreach (var requirement in context.ClientRequirements.Take(8))
        {
            var keyword = LongestWord(requirement);
            if (keyword is not null)
            {
                checks.Add(new ContractFactCheck(
                    $"Client requirement: {Truncate(requirement, 70)}",
                    new[] { keyword },
                    IsCritical: false));
            }
        }

        return checks;
    }

    private static void AddText(List<ContractFactCheck> checks, string description, string? value, bool critical = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        checks.Add(new ContractFactCheck($"{description} ({value.Trim()})", new[] { value.Trim() }, critical));
    }

    private static void AddMoney(
        List<ContractFactCheck> checks, string description, decimal? amount, string currency, bool critical = false)
    {
        if (amount is null)
        {
            return;
        }

        // Accept any formatting the model might reasonably choose for the same number.
        var matchers = new List<string>
        {
            amount.Value.ToString("N2", CultureInfo.InvariantCulture),
            amount.Value.ToString("0.##", CultureInfo.InvariantCulture),
            amount.Value.ToString("0", CultureInfo.InvariantCulture)
        };

        checks.Add(new ContractFactCheck(
            $"{description} ({amount.Value.ToString("N2", CultureInfo.InvariantCulture)} {currency})",
            matchers,
            critical));
    }

    private static string Flatten(ContractDraft draft)
    {
        var builder = new StringBuilder();
        builder.AppendLine(draft.Title).AppendLine(draft.Preamble);

        foreach (var section in draft.Sections)
        {
            builder.AppendLine(section.Title);
            foreach (var paragraph in section.Paragraphs)
            {
                builder.AppendLine(paragraph);
            }

            foreach (var item in section.Items)
            {
                builder.AppendLine(item);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Case- and separator-insensitive comparison so "EGP 1,500.00", "1500.00" and "1,500" all match
    /// the same underlying fact, and a stray comma in a name does not read as an omission.
    /// </summary>
    private static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch == '%' || ch == '.')
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        // Trailing ".00" carries no meaning for matching but blocks "1500.00" from matching "1500".
        var normalized = builder.ToString();
        return normalized.Replace(".00", string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// A distinctive token to check a free-text commitment by. Whole-phrase matching is too brittle
    /// (the model legitimately rewrites the vendor's phrasing into legal prose), but the longest
    /// content word survives that rewriting in practice.
    /// </summary>
    private static string? LongestWord(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var word = value
            .Split(new[] { ' ', ',', ';', ':', '.', '-', '/', '(', ')', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 5 && w.Any(char.IsLetter))
            .OrderByDescending(w => w.Length)
            .FirstOrDefault();

        return word;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "...";
}

/// <summary>One fact that must survive into the finished document, and how to recognise it there.</summary>
internal sealed record ContractFactCheck(
    string Description,
    IReadOnlyList<string> Matchers,
    bool IsCritical);

internal sealed record ContractValidationResult(
    IReadOnlyList<string> MissingCritical,
    IReadOnlyList<string> MissingImportant)
{
    public bool IsValid => MissingCritical.Count == 0 && MissingImportant.Count == 0;

    public IReadOnlyList<string> AllMissing => MissingCritical.Concat(MissingImportant).ToList();
}
