namespace Planura.Core.Application.Models;

/// <summary>
/// The booking choices needed to price a booking that has not been submitted yet. Deliberately
/// narrower than <see cref="AgreementPreviewRequestDto"/>: pricing depends only on which package and
/// slot were chosen, so quoting is a cheap, AI-free call the checkout can make as soon as the client
/// has picked a date — well before the contract is drafted.
/// </summary>
public class BookingPaymentQuoteRequestDto
{
    public long EventPlanId { get; set; }
    public long AvailabilityId { get; set; }
    public long? VendorPackageId { get; set; }
    public int? GuestCount { get; set; }
}
