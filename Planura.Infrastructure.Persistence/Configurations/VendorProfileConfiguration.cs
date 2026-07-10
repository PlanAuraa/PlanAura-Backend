using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planura.Core.Domain.Entities.Vendors;

namespace Planura.Infrastructure.Persistence.Configurations
{
    public class VendorProfileConfiguration : IEntityTypeConfiguration<VendorProfile>
    {
        public void Configure(EntityTypeBuilder<VendorProfile> builder)
        {
            builder.HasKey(v => v.Id);

            builder.Property(v => v.BusinessName)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(v => v.BusinessDescription)
                .HasMaxLength(2000);

            builder.Property(v => v.BusinessType)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(v => v.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.HasIndex(v => v.UserId).IsUnique();

            builder.HasOne(v => v.User)
                .WithOne()
                .HasForeignKey<VendorProfile>(v => v.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(v => v.Category)
                .WithMany(c => c.VendorProfiles)
                .HasForeignKey(v => v.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Restrict (not Cascade) to avoid a multiple-cascade-paths cycle with
            // VerificationRequest.VendorProfileId, which cascades the other direction.
            builder.HasOne(v => v.CurrentVerificationRequest)
                .WithMany()
                .HasForeignKey(v => v.CurrentVerificationRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.OwnsOne(v => v.Location, location =>
            {
                location.Property(l => l.City).HasMaxLength(100).IsRequired();
                location.Property(l => l.Area).HasMaxLength(100).IsRequired();
                location.Property(l => l.AddressLine).HasMaxLength(300).IsRequired();
            });
        }
    }
}
