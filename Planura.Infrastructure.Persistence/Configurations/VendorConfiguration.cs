using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planura.Core.Domain.Entities;

namespace Planura.Infrastructure.Persistence.Configurations;

public class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        builder.Property(vendor => vendor.BusinessName).HasMaxLength(200).IsRequired();
        builder.Property(vendor => vendor.City).HasMaxLength(100);
        builder.Property(vendor => vendor.Address).HasMaxLength(300);
        builder.Property(vendor => vendor.Latitude).HasPrecision(9, 6);
        builder.Property(vendor => vendor.Longitude).HasPrecision(9, 6);
        builder.Property(vendor => vendor.CoverImageUrl).HasMaxLength(500);
        builder.Property(vendor => vendor.LogoUrl).HasMaxLength(500);
        builder.Property(vendor => vendor.VerificationStatus).HasMaxLength(20).HasDefaultValue("unverified");
        builder.Property(vendor => vendor.AvgRating).HasPrecision(3, 2);
        builder.Property(vendor => vendor.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(vendor => vendor.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(vendor => vendor.UserId).IsUnique();
        builder.HasIndex(vendor => vendor.CategoryId);
        builder.HasIndex(vendor => vendor.City);
        builder.HasIndex(vendor => vendor.VerificationStatus);

        builder.HasOne(vendor => vendor.User)
            .WithOne(user => user.Vendor)
            .HasForeignKey<Vendor>(vendor => vendor.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(vendor => vendor.Category)
            .WithMany(category => category.Vendors)
            .HasForeignKey(vendor => vendor.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
