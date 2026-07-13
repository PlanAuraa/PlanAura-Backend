using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Planura.Core.Domain.Entities;
using Planura.Infrastructure.Persistence.Extensions;

namespace Planura.Infrastructure.Persistence;

public class PlanuraDbContext : IdentityDbContext<ApplicationUser, IdentityRole<long>, long>
{
    public PlanuraDbContext(DbContextOptions<PlanuraDbContext> options)
        : base(options)
    {
    }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
    public DbSet<VendorVerification> VendorVerifications => Set<VendorVerification>();
    public DbSet<VendorVerificationHistory> VendorVerificationHistory => Set<VendorVerificationHistory>();
    public DbSet<VendorVerificationDocument> VendorVerificationDocuments { get; set; }
    public DbSet<VendorPackage> VendorPackages => Set<VendorPackage>();
    public DbSet<VendorAvailability> VendorAvailability => Set<VendorAvailability>();
    public DbSet<EventPlan> EventPlans => Set<EventPlan>();
    public DbSet<EventPlanItem> EventPlanItems => Set<EventPlanItem>();
    public DbSet<BookingRequest> BookingRequests => Set<BookingRequest>();
    public DbSet<BookingStatusHistory> BookingStatusHistory => Set<BookingStatusHistory>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ReviewResponse> ReviewResponses => Set<ReviewResponse>();
    public DbSet<AiEventVisualization> AiEventVisualizations => Set<AiEventVisualization>();
    public DbSet<AiInvitation> AiInvitations => Set<AiInvitation>();
    public DbSet<PortfolioMedia> PortfolioMedia => Set<PortfolioMedia>();
    public DbSet<PortfolioLink> PortfolioLinks => Set<PortfolioLink>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureIdentityTables(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(PlanuraDbContext).Assembly);
        builder.UseSnakeCaseNames();
    }

    private static void ConfigureIdentityTables(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<IdentityRole<long>>().ToTable("roles");
        builder.Entity<IdentityUserRole<long>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<long>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<long>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<long>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<long>>().ToTable("role_claims");

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.FullName).HasMaxLength(150).IsRequired();
            entity.Property(user => user.PreferredLanguage).HasMaxLength(5).HasDefaultValue("ar");
            entity.Property(user => user.IsActive).HasDefaultValue(true);
            entity.Property(user => user.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(user => user.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
        });
    }
}
