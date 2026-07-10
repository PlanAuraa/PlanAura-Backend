using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planura.Core.Domain.Entities.Vendors;

namespace Planura.Infrastructure.Persistence.Configurations
{
    public class VendorCategoryConfiguration : IEntityTypeConfiguration<VendorCategory>
    {
        public void Configure(EntityTypeBuilder<VendorCategory> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Name)
                .HasMaxLength(100)
                .IsRequired();

            builder.HasIndex(c => c.Name).IsUnique();

            builder.HasData(
                new { Id = new Guid("7f2a1c1e-0001-4a00-8000-000000000001"), Name = "Venue", IsActive = true },
                new { Id = new Guid("7f2a1c1e-0001-4a00-8000-000000000002"), Name = "Photographer", IsActive = true },
                new { Id = new Guid("7f2a1c1e-0001-4a00-8000-000000000003"), Name = "Decorator", IsActive = true },
                new { Id = new Guid("7f2a1c1e-0001-4a00-8000-000000000004"), Name = "Caterer", IsActive = true },
                new { Id = new Guid("7f2a1c1e-0001-4a00-8000-000000000005"), Name = "DJ", IsActive = true },
                new { Id = new Guid("7f2a1c1e-0001-4a00-8000-000000000006"), Name = "MakeupArtist", IsActive = true }
            );
        }
    }
}
