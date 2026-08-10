using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planura.Core.Domain.Entities;

namespace Planura.Infrastructure.Persistence.Configurations;

public class VendorPayoutConfiguration : IEntityTypeConfiguration<VendorPayout>
{
    public void Configure(EntityTypeBuilder<VendorPayout> builder)
    {
        builder.Property(payout => payout.Amount).HasPrecision(12, 2);
        builder.Property(payout => payout.Reference).HasMaxLength(100);
        builder.Property(payout => payout.Notes).HasMaxLength(1000);
        builder.Property(payout => payout.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(payout => payout.VendorId);

        builder.HasOne(payout => payout.Vendor)
            .WithMany(vendor => vendor.Payouts)
            .HasForeignKey(payout => payout.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(payout => payout.RecordedByAdmin)
            .WithMany()
            .HasForeignKey(payout => payout.RecordedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
