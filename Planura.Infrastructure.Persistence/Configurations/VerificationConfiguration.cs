using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planura.Core.Domain.Entities;

namespace Planura.Infrastructure.Persistence.Configurations;

public class VendorVerificationConfiguration : IEntityTypeConfiguration<VendorVerification>
{
    public void Configure(EntityTypeBuilder<VendorVerification> builder)
    {
        builder.Property(verification => verification.Status).HasMaxLength(20).IsRequired();
        builder.Property(verification => verification.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(verification => verification.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(verification => verification.IsCurrent)
    .HasDefaultValue(true);

        builder.HasOne(verification => verification.Vendor)
            .WithMany(vendor => vendor.Verifications)
            .HasForeignKey(verification => verification.VendorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(verification => verification.ReviewedByAdmin)
            .WithMany(user => user.ReviewedVendorVerifications)
            .HasForeignKey(verification => verification.ReviewedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class VendorVerificationHistoryConfiguration : IEntityTypeConfiguration<VendorVerificationHistory>
{
    public void Configure(EntityTypeBuilder<VendorVerificationHistory> builder)
    {
        builder.Property(history => history.PreviousStatus).HasMaxLength(20);
        builder.Property(history => history.NewStatus).HasMaxLength(20).IsRequired();
        builder.Property(history => history.ChangedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(history => history.VendorVerification)
            .WithMany(verification => verification.History)
            .HasForeignKey(history => history.VendorVerificationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(history => history.ChangedByAdmin)
            .WithMany(user => user.VendorVerificationChanges)
            .HasForeignKey(history => history.ChangedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
