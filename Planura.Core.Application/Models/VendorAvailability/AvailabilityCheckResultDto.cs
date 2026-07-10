namespace Planura.Core.Application.Models;

public class AvailabilityCheckResultDto
{
    public long VendorId { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public bool IsAvailable { get; set; }
    public string Message { get; set; } = null!;
}
