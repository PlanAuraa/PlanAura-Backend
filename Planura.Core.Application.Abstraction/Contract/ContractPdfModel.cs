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

        /// <summary>Gemini-generated contract text, formatted with "SECTION n: TITLE" headings.</summary>
        public string ContractBody { get; set; } = null!;

        public ContractPartyDto PartyA { get; set; } = null!;
        public ContractPartyDto PartyB { get; set; } = null!;

        /// <summary>Optional label/value facts shown in the cover page summary card. Empty = card is omitted.</summary>
        public IReadOnlyList<ContractSummaryItem> SummaryItems { get; set; } = Array.Empty<ContractSummaryItem>();
    }
}
