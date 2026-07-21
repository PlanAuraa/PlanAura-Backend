namespace Planura.Core.Application.Abstraction.Contract
{
    /// <summary>
    /// Plain data describing an event booking, passed to <see cref="IGeminiService"/> so it can
    /// draft the contract text. Any field the client did not supply is left null/empty here -
    /// the provider is responsible for substituting "N/A" rather than inventing a value.
    /// </summary>
    public class ContractContentRequest
    {
        public string ContractId { get; set; } = null!;
        public DateTimeOffset GeneratedDate { get; set; }

        public string ClientName { get; set; } = null!;
        public string? ClientEmail { get; set; }
        public string? ClientPhone { get; set; }
        public string? ClientAddress { get; set; }
        public string? ClientRepresentativeName { get; set; }

        public string VendorName { get; set; } = null!;
        public string? VendorEmail { get; set; }
        public string? VendorPhone { get; set; }
        public string? VendorAddress { get; set; }
        public string? VendorRepresentativeName { get; set; }

        public string EventType { get; set; } = null!;
        public DateOnly EventDate { get; set; }
        public string? EventLocation { get; set; }
        public int? GuestCount { get; set; }

        public decimal Price { get; set; }
        public string Currency { get; set; } = "EGP";

        public string? AdditionalTerms { get; set; }
    }
}
