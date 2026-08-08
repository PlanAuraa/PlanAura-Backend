using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planura.Core.Domain.Entities;

namespace Planura.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(payment => payment.Amount).HasPrecision(12, 2);
        builder.Property(payment => payment.DepositAmount).HasPrecision(12, 2);
        builder.Property(payment => payment.TotalAmount).HasPrecision(12, 2);
        builder.Property(payment => payment.Status)
            .HasConversion<string>()
            .HasMaxLength(30) // widened from 20 to fit "DepositPaid_RemainderDue" (24 chars) with headroom
            .IsRequired();
        builder.Property(payment => payment.PaymentMethod).HasMaxLength(30);
        builder.Property(payment => payment.GatewayReference).HasMaxLength(100);
        builder.Property(payment => payment.SavedPaymentMethodId).HasMaxLength(100);
        builder.Property(payment => payment.RemainderGatewayReference).HasMaxLength(100);
        builder.Property(payment => payment.RemainderFailureReason).HasMaxLength(500);
        builder.Property(payment => payment.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(payment => payment.BookingRequestId);
        builder.HasIndex(payment => payment.GatewayReference);

        builder.HasOne(payment => payment.BookingRequest)
            .WithMany(booking => booking.Payments)
            .HasForeignKey(payment => payment.BookingRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(payment => payment.Client)
            .WithMany()
            .HasForeignKey(payment => payment.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(payment => payment.Vendor)
            .WithMany()
            .HasForeignKey(payment => payment.VendorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
