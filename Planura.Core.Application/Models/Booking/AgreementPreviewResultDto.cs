namespace Planura.Core.Application.Models;

/// <summary>
/// The generated Booking Agreement for review at the payment step. <see cref="Token"/> is passed
/// back on "Confirm &amp; Book" to bind this exact contract to the new booking;
/// <see cref="DocumentUrl"/> is the absolute URL the client's embedded viewer loads.
/// </summary>
public class AgreementPreviewResultDto
{
    public string Token { get; set; } = null!;
    public string ContractId { get; set; } = null!;
    public string DocumentUrl { get; set; } = null!;
    public DateTimeOffset GeneratedAt { get; set; }

    /// <summary>
    /// What this booking will actually cost and how much is taken now, resolved from the same package
    /// price the contract was drafted against. Returned alongside the agreement so the amounts the
    /// client reads in the contract and the amounts shown at checkout cannot disagree.
    /// </summary>
    public BookingPaymentQuoteDto PaymentPlan { get; set; } = new();
}
