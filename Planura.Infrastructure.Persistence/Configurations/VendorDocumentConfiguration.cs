using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planura.Core.Domain.Entities.Vendors;

namespace Planura.Infrastructure.Persistence.Configurations
{
    public class VendorDocumentConfiguration : IEntityTypeConfiguration<VendorDocument>
    {
        public void Configure(EntityTypeBuilder<VendorDocument> builder)
        {
            builder.HasKey(d => d.Id);

            builder.Property(d => d.DocumentType)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            builder.Property(d => d.StoredPath)
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(d => d.ContentType)
                .HasMaxLength(100)
                .IsRequired();

            builder.HasOne(d => d.VerificationRequest)
                .WithMany(vr => vr.Documents)
                .HasForeignKey(d => d.VerificationRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
