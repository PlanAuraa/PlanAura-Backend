using Planura.Core.Domain.Enums;

namespace Planura.Core.Application.Models;

public class BookingRequestDto
{
    public long Id { get; set; }
    public long EventPlanId { get; set; }
    public long ClientId { get; set; }
    public long VendorId { get; set; }
    public long? VendorPackageId { get; set; }
    public DateOnly EventDate { get; set; }
    public int? GuestCount { get; set; }
    public decimal? AgreedPrice { get; set; }
    public string? ClientMessage { get; set; }
    public BookingStatus Status { get; set; }
    public BookingPaymentStatus PaymentStatus { get; set; }
    public string? VendorResponse { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DisputeStatus? DisputeStatus { get; set; }
    public DateTimeOffset? DisputedAt { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}
