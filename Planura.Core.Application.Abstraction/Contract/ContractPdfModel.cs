namespace Planura.Core.Application.Abstraction.Contract
{
    /// <summary>
    /// Fully-resolved data handed to <see cref="IPdfService"/>. Generic across contract types
    /// (an event booking contract between a Client and a Vendor, a Vendor Partnership Agreement
    /// between Planura and a Vendor, or any future two-party agreement) so every contract renders
    /// through the exact same premium, branded QuestPDF template.
    /// This service is only responsible for PDF layout - it knows nothing about Gemini or prompts.
    /// </summary>
    public class ContractPdfModel
    {
        public string ContractId { get; set; } = null!;
        public DateTimeOffset GeneratedDate { get; set; }

        /// <summary>Big title shown on the cover page and page header, e.g. "Event Booking Contract".</summary>
        public string DocumentTitle { get; set; } = null!;

        /// <summary>One-line subtitle shown under the title on the cover page hero band.</summary>
        public string DocumentTagline { get; set; } = null!;

        /// <summary>Short line introducing the contract at the top of the first content page.</summary>
        public string IntroParagraph { get; set; } = null!;

        /// <summary>
        /// Gemini-generated contract text, formatted with "SECTION n: TITLE" headings. Used only when
        /// <see cref="Sections"/> is empty - i.e. for document types still generated as free prose.
        /// </summary>
        public string ContractBody { get; set; } = string.Empty;

        /// <summary>
        /// Pre-structured clauses. When non-empty these are rendered directly and
        /// <see cref="ContractBody"/> is ignored, so clause count and shape can vary per contract
        /// instead of being recovered from a fixed heading format.
        /// </summary>
        public IReadOnlyList<ContractSectionContent> Sections { get; set; } = Array.Empty<ContractSectionContent>();

        public ContractPartyDto PartyA { get; set; } = null!;
        public ContractPartyDto PartyB { get; set; } = null!;

        /// <summary>Optional label/value facts shown in the cover page summary card. Empty = card is omitted.</summary>
        public IReadOnlyList<ContractSummaryItem> SummaryItems { get; set; } = Array.Empty<ContractSummaryItem>();
    }
}
