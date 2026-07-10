using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planura.Core.Domain.Entities.Identity;

namespace Planura.Infrastructure.Persistence.Configurations
{
    public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.HasKey(r => r.Id);

            builder.Property(r => r.TokenHash)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(r => r.CreatedByIp)
                .HasMaxLength(64);

            builder.Property(r => r.ReplacedByTokenHash)
                .HasMaxLength(200);

            builder.Property(r => r.ReasonRevoked)
                .HasMaxLength(200);

            builder.HasIndex(r => r.TokenHash);
            builder.HasIndex(r => r.UserId);

            builder.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Ignore(r => r.IsExpired);
            builder.Ignore(r => r.IsActive);
        }
    }
}
