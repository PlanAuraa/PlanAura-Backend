using Planura.Core.Domain.Enums;

namespace Planura.Core.Domain.Entities;

public class VendorAvailability
{
    public long Id { get; set; }
    public long VendorId { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public AvailabilityStatus Status { get; set; } = AvailabilityStatus.Available;
    public long? BookingRequestId { get; set; }
    public DateTimeOffset? HoldExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public byte[] RowVersion { get; set; } = null!;

    public Vendor Vendor { get; set; } = null!;
    public BookingRequest? BookingRequest { get; set; }
}
