using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planura.Core.Domain.Entities;

namespace Planura.Infrastructure.Persistence.Configurations;

public class ServiceCategoryConfiguration : IEntityTypeConfiguration<ServiceCategory>
{
    public void Configure(EntityTypeBuilder<ServiceCategory> builder)
    {
        builder.Property(category => category.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(category => category.Slug).HasMaxLength(100).IsRequired();
        builder.Property(category => category.IconUrl).HasMaxLength(500);
        builder.Property(category => category.IsActive).HasDefaultValue(true);
        builder.Property(category => category.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(category => category.Slug).IsUnique();
    }
}

public class VendorPackageConfiguration : IEntityTypeConfiguration<VendorPackage>
{
    public void Configure(EntityTypeBuilder<VendorPackage> builder)
    {
        builder.Property(package => package.Title).HasMaxLength(200).IsRequired();
        builder.Property(package => package.BasePrice).HasPrecision(12, 2);
        builder.Property(package => package.Currency).HasMaxLength(3).HasDefaultValue("EGP");
        builder.Property(package => package.IsActive).HasDefaultValue(true);
        builder.Property(package => package.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(package => package.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(package => package.VendorId);

        builder.HasOne(package => package.Vendor)
            .WithMany(vendor => vendor.Packages)
            .HasForeignKey(package => package.VendorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class VendorAvailabilityConfiguration : IEntityTypeConfiguration<VendorAvailability>
{
    public void Configure(EntityTypeBuilder<VendorAvailability> builder)
    {
        builder.Property(availability => availability.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(availability => availability.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.Property(availability => availability.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(availability => new { availability.VendorId, availability.StartAt });

        builder.HasIndex(availability => new { availability.VendorId, availability.StartAt })
            .IsUnique()
            .HasFilter("([status] = 'Held' OR [status] = 'Booked')")
            .HasDatabaseName("idx_vendor_availability_no_double_hold");

        builder.HasOne(availability => availability.Vendor)
            .WithMany(vendor => vendor.Availability)
            .HasForeignKey(availability => availability.VendorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(availability => availability.BookingRequest)
            .WithMany(booking => booking.VendorAvailability)
            .HasForeignKey(availability => availability.BookingRequestId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
