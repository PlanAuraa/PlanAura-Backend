namespace Planura.Core.Application.Abstraction.Contract
{
    /// <summary>
    /// One rendered clause of a contract, already structured rather than parsed back out of prose.
    /// When <see cref="ContractPdfModel.Sections"/> is populated the PDF renders from these directly,
    /// which is what allows clause lists to be laid out as real enumerated items and lets the number
    /// of clauses vary per contract. The free-text <see cref="ContractPdfModel.ContractBody"/> path
    /// remains for documents that are still generated as prose.
    /// </summary>
    public class ContractSectionContent
    {
        public string Title { get; set; } = string.Empty;

        /// <summary>Operative prose for the clause.</summary>
        public List<string> Paragraphs { get; set; } = new();

        /// <summary>Enumerated points, rendered as (a), (b), (c)…</summary>
        public List<string> Items { get; set; } = new();
    }
}
