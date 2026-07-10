using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planura.Core.Domain.Entities;

namespace Planura.Infrastructure.Persistence.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.Property(client => client.City).HasMaxLength(100);
        builder.Property(client => client.AvatarUrl).HasMaxLength(500);
        builder.Property(client => client.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(client => client.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(client => client.UserId).IsUnique();

        builder.HasOne(client => client.User)
            .WithOne(user => user.Client)
            .HasForeignKey<Client>(client => client.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
