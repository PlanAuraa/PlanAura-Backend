using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;

namespace Planura.Infrastructure.Persistence.Configurations;

public class BookingRequestConfiguration : IEntityTypeConfiguration<BookingRequest>
{
    public void Configure(EntityTypeBuilder<BookingRequest> builder)
    {
        builder.Property(booking => booking.AgreedPrice).HasPrecision(12, 2);
        builder.Property(booking => booking.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(BookingStatus.Pending)
            .IsRequired();
        builder.Property(booking => booking.PaymentStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(BookingPaymentStatus.Unpaid)
            .IsRequired();
        builder.Property(booking => booking.DisputeStatus)
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(booking => booking.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(booking => booking.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(booking => booking.VendorId);
        builder.HasIndex(booking => booking.ClientId);
        builder.HasIndex(booking => booking.Status);

        builder.HasOne(booking => booking.EventPlan)
            .WithMany(plan => plan.BookingRequests)
            .HasForeignKey(booking => booking.EventPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(booking => booking.Client)
            .WithMany(client => client.BookingRequests)
            .HasForeignKey(booking => booking.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(booking => booking.Vendor)
            .WithMany(vendor => vendor.BookingRequests)
            .HasForeignKey(booking => booking.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(booking => booking.VendorPackage)
            .WithMany(package => package.BookingRequests)
            .HasForeignKey(booking => booking.VendorPackageId)
            .OnDelete(DeleteBehavior.SetNull);

        // Restrict (not SetNull): two cascading FKs from this table to Users would trigger
        // SQL Server error 1785 ("multiple cascade paths"). ApplicationUser rows are never
        // hard-deleted in this app (suspension flips IsActive), so Restrict is safe here.
        builder.HasOne(booking => booking.DisputedByUser)
            .WithMany(user => user.DisputedBookingRequests)
            .HasForeignKey(booking => booking.DisputedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(booking => booking.ResolvedByAdmin)
            .WithMany(user => user.ResolvedBookingRequests)
            .HasForeignKey(booking => booking.ResolvedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BookingStatusHistoryConfiguration : IEntityTypeConfiguration<BookingStatusHistory>
{
    public void Configure(EntityTypeBuilder<BookingStatusHistory> builder)
    {
        builder.Property(history => history.PreviousStatus).HasMaxLength(20);
        builder.Property(history => history.NewStatus).HasMaxLength(20).IsRequired();
        builder.Property(history => history.ChangedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(history => history.BookingRequest)
            .WithMany(booking => booking.StatusHistory)
            .HasForeignKey(history => history.BookingRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(history => history.ChangedByUser)
            .WithMany(user => user.BookingStatusChanges)
            .HasForeignKey(history => history.ChangedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
