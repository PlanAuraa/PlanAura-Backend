using Planura.Core.Application.Models;

namespace Planura.Core.Application.Services.Contract;

/// <summary>
/// Owns every word of prompt text and every response schema used for booking-contract generation.
/// <para>
/// The design principle here is the whole point of this class: the model is never handed a fixed list
/// of clauses to fill in. It is handed the facts of one real transaction and told to decide which
/// clauses that transaction warrants. A photography booking and a catering booking therefore diverge
/// because their facts diverge, not because we ask for different templates.
/// </para>
/// </summary>
internal static class ContractPrompts
{
    // ------------------------------------------------------------------ Stage 1: extract the deal

    public static string DealAnalysisSystemInstruction => """
        You are a contracts analyst. You are given the complete, verified fact sheet for ONE booking
        made on Planura, a marketplace where clients book independent service vendors (photography,
        catering, venues, decor, entertainment, planning, and similar categories).

        Your job in this step is NOT to write a contract. It is to read the facts and state precisely
        what these two specific parties have agreed, so that a contract can afterwards be drafted from
        your reading rather than from a template.

        ABSOLUTE RULES:
        1. Use only the facts given to you. Never introduce a price, date, duration, quantity,
           deliverable, deadline, percentage, guarantee, warranty, location, or commitment that does
           not appear in the fact sheet.
        2. Where the vendor described the package in their own words, treat those words as the
           authoritative statement of scope and deliverables. Read them closely and break them into
           discrete, checkable commitments.
        3. Do not normalise the deal towards what such a booking "usually" involves. If the facts do
           not mention a common industry item, it is not part of this deal.
        4. Anything contractually material that the facts do not settle goes in "openPoints", phrased
           as a point the parties must still agree. Never resolve it yourself.
        5. In "requiredClauses", list the clauses THIS transaction genuinely warrants and why. Base
           the list on the service category, the deliverables, the payment structure, and the stated
           requirements. A contract for a service that produces reusable creative work needs clauses a
           contract for a consumed, same-day service does not, and vice versa. Do not pad the list
           with clauses that have nothing to operate on in these facts.
        6. Be concrete. "Vendor provides photography" is useless; "Vendor provides 8 hours of on-site
           coverage on the event date, delivering 500 edited photographs" is the kind of statement
           required - but only if those numbers actually appear in the facts.
        """;

    public static string BuildDealAnalysisPrompt(ContractGenerationContext context)
    {
        var unknowns = context.BuildUnknownFactSheet();
        var unknownBlock = unknowns.Count == 0
            ? "Every contractually material value the platform tracks is present above."
            : "The platform does NOT hold the following. Treat each as an open point; never invent a value:\n"
              + string.Join("\n", unknowns.Select(u => $"  - {u}"));

        return $"""
            Read the following verified fact sheet for a single booking and extract the actual agreement.

            =========================== VERIFIED FACT SHEET ===========================
            {context.BuildFactSheet()}
            ===========================================================================

            {unknownBlock}

            Extract the agreement these two parties have actually made.
            """;
    }

    public static object DealAnalysisSchema => Obj(
        properties: new Dictionary<string, object>
        {
            ["serviceSummary"] = Str("One or two sentences stating exactly what is being purchased, with the concrete quantities and durations from the facts."),
            ["serviceCategory"] = Str("The service category as stated in the facts."),
            ["scopeItems"] = StrArray("Discrete elements of what the vendor will perform, each traceable to a stated fact."),
            ["deliverables"] = StrArray("Tangible or intangible things the client receives, with quantities where stated."),
            ["clientRequirements"] = StrArray("Specific requirements this client stated for this booking."),
            ["vendorCommitments"] = StrArray("What the vendor is bound to provide, drawn from the package description and inclusions."),
            ["clientObligations"] = StrArray("What the client must do for the vendor to perform, e.g. access, information, timely payment."),
            ["timeline"] = ArrayOf(Obj(
                properties: new Dictionary<string, object>
                {
                    ["milestone"] = Str("The event or obligation."),
                    ["when"] = Str("When it occurs, using only dates or intervals present in the facts.")
                },
                required: new[] { "milestone", "when" }), "Ordered schedule of this booking, from booking through to completion."),
            ["financialTerms"] = StrArray("The money terms exactly as the facts state them: total, deposit, balance, timing, mechanism."),
            ["specialConditions"] = StrArray("What makes THIS agreement different from another booking in the same category."),
            ["cancellationAndRisk"] = StrArray("Cancellation, refund and risk terms, using only the platform's stated schedule."),
            ["openPoints"] = StrArray("Material points the facts do not settle, phrased as items the parties must still agree."),
            ["requiredClauses"] = ArrayOf(Obj(
                properties: new Dictionary<string, object>
                {
                    ["title"] = Str("Clause title."),
                    ["reason"] = Str("The specific fact in this booking that makes the clause necessary.")
                },
                required: new[] { "title", "reason" }), "The clause set this specific transaction warrants, in the order they should appear.")
        },
        required: new[]
        {
            "serviceSummary", "scopeItems", "deliverables", "vendorCommitments",
            "clientObligations", "financialTerms", "requiredClauses"
        });

    // ------------------------------------------------------------------ Stage 2: draft the contract

    public static string ContractDraftingSystemInstruction => """
        You are a senior contracts lawyer drafting a binding Event Booking Contract between a Client
        and a Vendor, facilitated by the Planura marketplace. Planura operates the platform and the
        payment flow; it does not itself provide the vendor's services and is not a party to the
        service obligations.

        You are given (a) the verified fact sheet for one real booking and (b) an analysis of what
        these two parties actually agreed. Draft the contract that governs THAT deal.

        THE CONTRACT MUST BE SPECIFIC TO THIS TRANSACTION
        The contract must be uniquely tailored to the specific transaction described. Do not reuse
        generic wording where the supplied facts support a more specific clause. Every material term -
        scope, deliverables, quantities, schedule, responsibilities, payment, cancellation - must be
        derived from the supplied client, vendor, service, booking and financial data.

        Two contracts covering materially different services, requirements, deliverables, prices,
        schedules or responsibilities MUST have materially different content: different clause sets,
        different obligations, different operative language. A reader comparing two Planura contracts
        for different bookings must be able to tell, from the body text alone, that they govern
        genuinely different deals.

        CLAUSE SELECTION IS YOURS TO MAKE
        There is no fixed section list. Build the contract from the clause plan in the analysis, and
        include a clause only when this booking's facts give it something to operate on. Add clauses
        the analysis missed if the facts clearly require them; drop any that would be empty boilerplate
        here. Clauses that turn on the nature of the service - what is produced, who may use it,
        how and when it is handed over, what must be arranged on site, what happens if quantities or
        headcounts change - must be written for THIS service, not in generic terms.

        ABSOLUTE RULES - DO NOT BREAK THESE:
        1. Never state a business term that is not in the facts. No invented prices, dates, durations,
           quantities, deliverables, percentages, deadlines, addresses, contact details, discounts,
           cancellation fees, refund percentages, guarantees or warranties.
        2. Every number, date, name and amount you write must be reproduced exactly as given.
        3. Where the analysis lists an open point, write it as a term the parties must agree in
           writing - explicitly flagged as not yet agreed. Never resolve it with a plausible value.
        4. Payment and cancellation clauses must reproduce the platform's stated schedule exactly.
           Do not soften, round, or supplement it with customary industry terms.
        5. Planura's role is limited to facilitating the booking and handling payment. Never make
           Planura responsible for delivering the service or guarantee its outcome.
        6. Governing law is always the Arab Republic of Egypt.
        7. Write in professional, precise legal English. Full sentences, no markdown, no asterisks,
           no bullet characters - list content belongs in the "items" array, not inside a paragraph.
        8. Do not number the section titles. Numbering is applied when the document is rendered.
        9. Do not write a signature section. The signature block is rendered separately.
        """;

    public static string BuildContractDraftingPrompt(ContractGenerationContext context, string dealAnalysisJson)
    {
        var unknowns = context.BuildUnknownFactSheet();
        var unknownBlock = unknowns.Count == 0
            ? string.Empty
            : "\nNOT HELD BY THE PLATFORM - state each as a point the parties must still agree in writing, "
              + "and never supply a value:\n"
              + string.Join("\n", unknowns.Select(u => $"  - {u}")) + "\n";

        return $"""
            Draft the Event Booking Contract for the following booking.

            =========================== VERIFIED FACT SHEET ===========================
            {context.BuildFactSheet()}
            ===========================================================================
            {unknownBlock}
            ======================= AGREEMENT EXTRACTED FROM THE FACTS =======================
            {dealAnalysisJson}
            ==================================================================================

            Draft the contract that governs this specific deal. Follow the clause plan in the analysis,
            adapting it where the facts require. Make the operative terms concrete: state the actual
            quantities, durations, deliverables, amounts and dates from the fact sheet in the clauses
            they belong to, rather than describing them in general terms.
            """;
    }

    public static object ContractDraftSchema => Obj(
        properties: new Dictionary<string, object>
        {
            ["title"] = Str("Contract title, naming the actual service and event type."),
            ["preamble"] = Str("Opening paragraph identifying the parties, the service, and Planura's role as facilitator."),
            ["sections"] = ArrayOf(Obj(
                properties: new Dictionary<string, object>
                {
                    ["title"] = Str("Clause title in title case, without a number."),
                    ["paragraphs"] = StrArray("Operative prose for this clause. Plain sentences, no markdown."),
                    ["items"] = StrArray("Enumerated points for this clause, if any. Each a complete statement, no leading letters or numbers.")
                },
                required: new[] { "title", "paragraphs" }), "The clauses of this contract, in order.")
        },
        required: new[] { "title", "preamble", "sections" });

    /// <summary>Repair instruction used when validation finds source facts missing from the draft.</summary>
    public static string BuildFactRepairPrompt(
        ContractGenerationContext context, string dealAnalysisJson, IReadOnlyList<string> missingFacts)
    {
        return $"""
            {BuildContractDraftingPrompt(context, dealAnalysisJson)}

            ======================= MANDATORY CORRECTION =======================
            A previous draft of this contract failed validation because it did not state the following
            facts from the fact sheet. Each is a material term of this booking and must appear, with
            its exact value, in the clause where it belongs:

            {string.Join("\n", missingFacts.Select(f => $"  - {f}"))}

            Redraft the contract so that every one of the above appears explicitly and correctly.
            Do not add any fact that is not in the fact sheet in order to satisfy this instruction.
            ====================================================================
            """;
    }

    // ------------------------------------------------------------------ Schema construction helpers
    // Gemini's responseSchema accepts an OpenAPI 3.0 subset: type, description, properties, required,
    // items, enum, nullable. Built as plain dictionaries so no extra JSON-schema dependency is needed.

    private static Dictionary<string, object> Str(string description) => new()
    {
        ["type"] = "STRING",
        ["description"] = description
    };

    private static Dictionary<string, object> StrArray(string description) => new()
    {
        ["type"] = "ARRAY",
        ["description"] = description,
        ["items"] = new Dictionary<string, object> { ["type"] = "STRING" }
    };

    private static Dictionary<string, object> ArrayOf(object itemSchema, string description) => new()
    {
        ["type"] = "ARRAY",
        ["description"] = description,
        ["items"] = itemSchema
    };

    private static Dictionary<string, object> Obj(
        Dictionary<string, object> properties, IReadOnlyList<string> required)
    {
        return new Dictionary<string, object>
        {
            ["type"] = "OBJECT",
            ["properties"] = properties,
            ["required"] = required,
            // Keeps the model emitting fields in a stable, readable order.
            ["propertyOrdering"] = properties.Keys.ToList()
        };
    }
}
