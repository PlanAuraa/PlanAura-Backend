namespace Planura.Core.Application.Models;

public class UpdateVendorAvailabilityDto
{
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
}
