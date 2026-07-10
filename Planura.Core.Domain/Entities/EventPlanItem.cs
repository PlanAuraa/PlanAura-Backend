namespace Planura.Core.Domain.Entities;

public class EventPlanItem
{
    public long Id { get; set; }
    public long EventPlanId { get; set; }
    public long VendorId { get; set; }
    public long? VendorPackageId { get; set; }
    public decimal? EstimatedPrice { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public EventPlan EventPlan { get; set; } = null!;
    public Vendor Vendor { get; set; } = null!;
    public VendorPackage? VendorPackage { get; set; }
}
