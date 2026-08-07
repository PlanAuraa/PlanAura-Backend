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

public class EventPlanChecklistItemConfiguration : IEntityTypeConfiguration<EventPlanChecklistItem>
{
    public void Configure(EntityTypeBuilder<EventPlanChecklistItem> builder)
    {
        builder.Property(item => item.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        // A category can only be checked off once per plan.
        builder.HasIndex(item => new { item.EventPlanId, item.ServiceCategoryId }).IsUnique();

        builder.HasOne(item => item.EventPlan)
            .WithMany(plan => plan.ChecklistItems)
            .HasForeignKey(item => item.EventPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.ServiceCategory)
            .WithMany()
            .HasForeignKey(item => item.ServiceCategoryId)
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

public class AiChatConversationConfiguration : IEntityTypeConfiguration<AiChatConversation>
{
    public void Configure(EntityTypeBuilder<AiChatConversation> builder)
    {
        builder.Property(conversation => conversation.Title).HasMaxLength(200);
        builder.Property(conversation => conversation.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(conversation => conversation.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(conversation => conversation.Client)
            .WithMany(client => client.AiChatConversations)
            .HasForeignKey(conversation => conversation.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict (not SetNull): Client already cascades to both EventPlan and
        // AiChatConversation directly, so a SetNull path through EventPlan would be a
        // second cascade path to the same table, which SQL Server rejects outright.
        builder.HasOne(conversation => conversation.EventPlan)
            .WithMany(plan => plan.AiChatConversations)
            .HasForeignKey(conversation => conversation.EventPlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AiChatMessageConfiguration : IEntityTypeConfiguration<AiChatMessage>
{
    public void Configure(EntityTypeBuilder<AiChatMessage> builder)
    {
        builder.Property(message => message.Role).HasMaxLength(20).IsRequired();
        builder.Property(message => message.Content).IsRequired();
        builder.Property(message => message.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(message => message.Conversation)
            .WithMany(conversation => conversation.Messages)
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
