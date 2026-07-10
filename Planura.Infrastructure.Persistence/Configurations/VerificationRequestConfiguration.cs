using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planura.Core.Domain.Entities.Vendors;

namespace Planura.Infrastructure.Persistence.Configurations
{
    public class VerificationRequestConfiguration : IEntityTypeConfiguration<VerificationRequest>
    {
        public void Configure(EntityTypeBuilder<VerificationRequest> builder)
        {
            builder.HasKey(vr => vr.Id);

            builder.Property(vr => vr.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(vr => vr.DecisionReason)
                .HasMaxLength(1000);

            builder.Property(vr => vr.RowVersion)
                .IsRowVersion();

            builder.HasIndex(vr => new { vr.VendorProfileId, vr.SubmittedAt });

            builder.HasOne(vr => vr.VendorProfile)
                .WithMany(v => v.VerificationRequests)
                .HasForeignKey(vr => vr.VendorProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(vr => vr.ReviewedByUser)
                .WithMany()
                .HasForeignKey(vr => vr.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
