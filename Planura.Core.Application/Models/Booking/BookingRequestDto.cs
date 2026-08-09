using Planura.Core.Domain.Enums;

namespace Planura.Core.Application.Models;

public class BookingRequestDto
{
    public long Id { get; set; }
    public long EventPlanId { get; set; }
    public long ClientId { get; set; }
    public string? ClientName { get; set; }
    public long VendorId { get; set; }
    public long? VendorPackageId { get; set; }
    public DateOnly EventDate { get; set; }
    public int? GuestCount { get; set; }
    public decimal? AgreedPrice { get; set; }
    public string? ClientMessage { get; set; }

    // Deposit / partial-payment (Phase 3, surfaced for the client cancel warning). IsDeposit distinguishes a
    // deposit-path booking; DepositAmount/TotalAmount are the recorded split so the UI can show the exact
    // forfeited deposit on a deposit-only cancellation. Null/false on the full-payment path.
    public bool IsDeposit { get; set; }
    public decimal? DepositAmount { get; set; }
    public decimal? TotalAmount { get; set; }
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

    public string? ContractId { get; set; }
    public string? ContractDocumentUrl { get; set; }
    public DateTimeOffset? ContractGeneratedAt { get; set; }

    public DateTimeOffset? ClientAgreedAt { get; set; }
    public DateTimeOffset? VendorAgreedAt { get; set; }

    public DateTimeOffset? AwaitingConfirmationSince { get; set; }

    public string? CancellationReason { get; set; }
    public DateTimeOffset? CancellationRequestedAt { get; set; }
    public string? CancellationReviewNotes { get; set; }
    public DateTimeOffset? CancellationReviewedAt { get; set; }
    public decimal? CancellationRefundPercent { get; set; }
    public decimal? CancellationRefundAmount { get; set; }
    public RefundStatus RefundStatus { get; set; }

    /// <summary>The booked slot's exact start/end — lets every "date" display also show the time
    /// (vendor list/details/accept-dialog, and the client's own booking views).</summary>
    public DateTimeOffset? SlotStartAt { get; set; }
    public DateTimeOffset? SlotEndAt { get; set; }

    public long? ReviewId { get; set; }
    public int? ReviewRating { get; set; }
    public string? ReviewComment { get; set; }
}
