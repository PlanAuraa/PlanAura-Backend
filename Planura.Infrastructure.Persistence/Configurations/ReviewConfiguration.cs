using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planura.Core.Domain.Entities;

namespace Planura.Infrastructure.Persistence.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.Property(review => review.Rating).IsRequired();
        builder.Property(review => review.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(review => review.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(review => review.BookingRequestId).IsUnique();
        builder.HasIndex(review => review.VendorId);

        builder.HasOne(review => review.BookingRequest)
            .WithOne(booking => booking.Review)
            .HasForeignKey<Review>(review => review.BookingRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(review => review.Client)
            .WithMany(client => client.Reviews)
            .HasForeignKey(review => review.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(review => review.Vendor)
            .WithMany(vendor => vendor.Reviews)
            .HasForeignKey(review => review.VendorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ReviewResponseConfiguration : IEntityTypeConfiguration<ReviewResponse>
{
    public void Configure(EntityTypeBuilder<ReviewResponse> builder)
    {
        builder.Property(response => response.Response).IsRequired();
        builder.Property(response => response.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(response => response.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(response => response.ReviewId).IsUnique();

        builder.HasOne(response => response.Review)
            .WithOne(review => review.Response)
            .HasForeignKey<ReviewResponse>(response => response.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(response => response.Vendor)
            .WithMany(vendor => vendor.ReviewResponses)
            .HasForeignKey(response => response.VendorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
