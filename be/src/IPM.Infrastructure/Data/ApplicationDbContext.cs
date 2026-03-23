using IPM.Domain.Entities;
using IPM.Domain.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IPM.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly string _tablePrefix;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, DatabaseSettings? databaseSettings = null)
        : base(options)
    {
        _tablePrefix = databaseSettings?.TablePrefix ?? "123";
    }

    public DbSet<Member> Members => Set<Member>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<News> News => Set<News>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectTask> Tasks => Set<ProjectTask>();
    public DbSet<ProjectBudget> ProjectBudgets => Set<ProjectBudget>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();
    public DbSet<AiLog> AiLogs => Set<AiLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply all configurations
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Apply table prefix to all entities
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (tableName != null && !tableName.StartsWith("AspNet"))
            {
                entity.SetTableName($"{_tablePrefix}_{tableName}");
            }
        }
    }
}
