namespace Planura.Core.Application.Models;

public class CreateVendorAvailabilityDto
{
    public long VendorId { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
}
