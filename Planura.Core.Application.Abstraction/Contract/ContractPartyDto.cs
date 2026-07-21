namespace Planura.Core.Application.Abstraction.Contract
{
    /// <summary>
    /// One contracting party as rendered on the contract cover page and signature block. Used for
    /// both natural parties (a Client, a Vendor) and Planura itself acting as the marketplace
    /// operator, so the same PDF template works for any two-party agreement.
    /// </summary>
    public class ContractPartyDto
    {
        /// <summary>Short uppercase role label shown above the name, e.g. "CLIENT", "VENDOR", "PLANURA".</summary>
        public string Label { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? RepresentativeName { get; set; }
    }
}
