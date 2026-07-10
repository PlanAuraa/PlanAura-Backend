using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Planura.Core.Domain.Entities.Identity;
using Planura.Core.Domain.Entities.Vendors;

namespace Planura.Infrastructure.Persistence
{
    public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<VendorProfile> VendorProfiles => Set<VendorProfile>();
        public DbSet<VendorCategory> VendorCategories => Set<VendorCategory>();
        public DbSet<VendorImage> VendorImages => Set<VendorImage>();
        public DbSet<VerificationRequest> VerificationRequests => Set<VerificationRequest>();
        public DbSet<VendorDocument> VendorDocuments => Set<VendorDocument>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
