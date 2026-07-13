namespace Planura.Core.Application.Models;

public class CreateBookingRequestDto
{
    public long EventPlanId { get; set; }
    public long AvailabilityId { get; set; }
    public long? VendorPackageId { get; set; }
    public int? GuestCount { get; set; }
    public decimal? AgreedPrice { get; set; }
    public string? ClientMessage { get; set; }
}
