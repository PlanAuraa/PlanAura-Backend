using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planura.Core.Domain.Entities;

namespace Planura.Infrastructure.Persistence.Configurations;

public class PortfolioMediaConfiguration : IEntityTypeConfiguration<PortfolioMedia>
{
    public void Configure(EntityTypeBuilder<PortfolioMedia> builder)
    {
        builder.Property(media => media.MediaType).HasMaxLength(10).IsRequired();
        builder.Property(media => media.FileUrl).HasMaxLength(500).IsRequired();
        builder.Property(media => media.ThumbnailUrl).HasMaxLength(500);
        builder.Property(media => media.Title).HasMaxLength(200);
        builder.Property(media => media.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(media => media.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(media => media.VendorId);

        builder.HasOne(media => media.Vendor)
            .WithMany(vendor => vendor.PortfolioMedia)
            .HasForeignKey(media => media.VendorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PortfolioLinkConfiguration : IEntityTypeConfiguration<PortfolioLink>
{
    public void Configure(EntityTypeBuilder<PortfolioLink> builder)
    {
        builder.Property(link => link.Platform).HasMaxLength(20).IsRequired();
        builder.Property(link => link.Url).HasMaxLength(500).IsRequired();
        builder.Property(link => link.Title).HasMaxLength(200);
        builder.Property(link => link.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(link => link.VendorId);

        builder.HasOne(link => link.Vendor)
            .WithMany(vendor => vendor.PortfolioLinks)
            .HasForeignKey(link => link.VendorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
