using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planura.Core.Domain.Entities;

namespace Planura.Infrastructure.Persistence.Configurations;

public class EventPlanConfiguration : IEntityTypeConfiguration<EventPlan>
{
    public void Configure(EntityTypeBuilder<EventPlan> builder)
    {
        builder.Property(plan => plan.Title).HasMaxLength(200);
        builder.Property(plan => plan.EventType).HasMaxLength(50).IsRequired();
        builder.Property(plan => plan.City).HasMaxLength(100);
        builder.Property(plan => plan.BudgetTotal).HasPrecision(12, 2);
        builder.Property(plan => plan.Status).HasMaxLength(20).HasDefaultValue("draft");
        builder.Property(plan => plan.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(plan => plan.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(plan => plan.Client)
            .WithMany(client => client.EventPlans)
            .HasForeignKey(plan => plan.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class EventPlanItemConfiguration : IEntityTypeConfiguration<EventPlanItem>
{
    public void Configure(EntityTypeBuilder<EventPlanItem> builder)
    {
        builder.Property(item => item.EstimatedPrice).HasPrecision(12, 2);
        builder.Property(item => item.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(item => item.EventPlan)
            .WithMany(plan => plan.Items)
            .HasForeignKey(item => item.EventPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.Vendor)
            .WithMany(vendor => vendor.EventPlanItems)
            .HasForeignKey(item => item.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.VendorPackage)
            .WithMany(package => package.EventPlanItems)
            .HasForeignKey(item => item.VendorPackageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AiEventVisualizationConfiguration : IEntityTypeConfiguration<AiEventVisualization>
{
    public void Configure(EntityTypeBuilder<AiEventVisualization> builder)
    {
        builder.Property(visualization => visualization.ImageUrl).HasMaxLength(500);
        builder.Property(visualization => visualization.ModelUsed).HasMaxLength(50);
        builder.Property(visualization => visualization.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(visualization => visualization.EventPlan)
            .WithMany(plan => plan.AiEventVisualizations)
            .HasForeignKey(visualization => visualization.EventPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AiInvitationConfiguration : IEntityTypeConfiguration<AiInvitation>
{
    public void Configure(EntityTypeBuilder<AiInvitation> builder)
    {
        builder.Property(invitation => invitation.Theme).HasMaxLength(100);
        builder.Property(invitation => invitation.ImageUrl).HasMaxLength(500);
        builder.Property(invitation => invitation.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(invitation => invitation.EventPlan)
            .WithMany(plan => plan.AiInvitations)
            .HasForeignKey(invitation => invitation.EventPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
