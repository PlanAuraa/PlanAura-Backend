using Planura.Core.Domain.Enums;

namespace Planura.Core.Application.Models;

/// <summary>
/// The real financial state of an existing booking, derived from its <c>Payment</c> row.
/// <para>
/// Planura authorizes the card at submission and captures only when the vendor accepts, so
/// "authorized" and "paid" are genuinely different amounts for part of a booking's life. Keeping them
/// as separate fields is what lets the UI say "held, not yet charged" truthfully instead of implying
/// money has moved.
/// </para>
/// </summary>
public class BookingPaymentSummaryDto
{
    public long PaymentId { get; set; }

    public string Currency { get; set; } = "EGP";

    /// <summary>Full contract value, regardless of how much has been collected so far.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Amount held on the card but not yet captured. Zero once captured, refunded or voided.</summary>
    public decimal AmountAuthorized { get; set; }

    /// <summary>Amount actually captured. Zero while the payment is only authorized.</summary>
    public decimal AmountPaid { get; set; }

    /// <summary>Total less what has been captured. Equals the total while nothing has been charged.</summary>
    public decimal RemainingAmount { get; set; }

    /// <summary>True when this booking took the deposit path.</summary>
    public bool IsDeposit { get; set; }

    /// <summary>The deposit figure recorded at booking time, on the deposit path only.</summary>
    public decimal? DepositAmount { get; set; }

    /// <summary>Whether the platform will collect <see cref="RemainingAmount"/> automatically. See
    /// <see cref="BookingPaymentQuoteDto.RemainderCollectionScheduled"/> — currently always false.</summary>
    public bool RemainderCollectionScheduled { get; set; }

    public PaymentStatus Status { get; set; }

    /// <summary>
    /// Payment processor reference for this transaction, shown to the parties for support enquiries.
    /// Not a secret (the intent's client secret is never exposed) and already returned to clients by
    /// <c>GET /api/payments/my-transactions</c> via <see cref="PaymentDto.GatewayReference"/>.
    /// </summary>
    public string? Reference { get; set; }

    public DateTimeOffset? AuthorizedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? RefundedAt { get; set; }

    /// <summary>The amount actually returned to the client so far. Zero unless Status is Refunded or
    /// PartiallyRefunded.</summary>
    public decimal RefundedAmount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
