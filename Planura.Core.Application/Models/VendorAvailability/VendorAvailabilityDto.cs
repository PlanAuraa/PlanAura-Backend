using Planura.Core.Domain.Enums;

namespace Planura.Core.Application.Models;

public class VendorAvailabilityDto
{
    public long Id { get; set; }
    public long VendorId { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public AvailabilityStatus Status { get; set; }
    public long? BookingRequestId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
