namespace Planura.Core.Domain.Entities;

/// <summary>
/// A manual, out-of-band disbursement the admin recorded as having paid to a vendor (e.g. a bank transfer).
/// The platform does not automate vendor payouts - this is a settlement ledger entry, not a payment gateway
/// transaction, so it has no gateway reference of its own. See AdminVendorPayoutService for how the amount
/// currently owed to a vendor is computed from this ledger plus Payment.AmountCapturedExpression.
/// </summary>
public class VendorPayout
{
    public long Id { get; set; }
    public long VendorId { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset PayoutDate { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public long RecordedByAdminId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Vendor Vendor { get; set; } = null!;
    public ApplicationUser RecordedByAdmin { get; set; } = null!;
}
