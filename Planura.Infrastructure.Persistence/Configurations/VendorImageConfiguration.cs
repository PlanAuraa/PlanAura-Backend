using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planura.Core.Domain.Entities.Vendors;

namespace Planura.Infrastructure.Persistence.Configurations
{
    public class VendorImageConfiguration : IEntityTypeConfiguration<VendorImage>
    {
        public void Configure(EntityTypeBuilder<VendorImage> builder)
        {
            builder.HasKey(i => i.Id);

            builder.Property(i => i.StoredPath)
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(i => i.ContentType)
                .HasMaxLength(100)
                .IsRequired();

            builder.HasOne(i => i.VendorProfile)
                .WithMany(v => v.Images)
                .HasForeignKey(i => i.VendorProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
