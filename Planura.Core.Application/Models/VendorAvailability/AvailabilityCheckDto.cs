namespace Planura.Core.Application.Models;

public class AvailabilityCheckDto
{
    public long VendorId { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
}
