namespace Planura.Core.Application.Models;

public class CreateVendorAvailabilityDto
{
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
}
