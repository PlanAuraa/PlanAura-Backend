using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planura.Core.Domain.Entities;

namespace Planura.Infrastructure.Persistence.Configurations;

public class VendorVerificationDocumentConfiguration
    : IEntityTypeConfiguration<VendorVerificationDocument>
{
    public void Configure(EntityTypeBuilder<VendorVerificationDocument> builder)
    {
        builder.Property(d => d.FileUrl)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(d => d.OriginalFileName)
            .HasMaxLength(255);

        builder.Property(d => d.ContentType)
            .HasMaxLength(100);

        builder.Property(d => d.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(d => d.VendorVerification)
            .WithMany(v => v.Documents)
            .HasForeignKey(d => d.VendorVerificationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}