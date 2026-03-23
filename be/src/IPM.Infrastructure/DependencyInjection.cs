using Hangfire;
using Hangfire.SqlServer;
using IPM.Application.Services.Interfaces;
using IPM.Domain.Interfaces;
using IPM.Infrastructure.Data;
using IPM.Infrastructure.Hubs;
using IPM.Domain.Identity;
using IPM.Infrastructure.Identity;
using IPM.Infrastructure.Jobs;
using IPM.Infrastructure.Repositories;
using IPM.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace IPM.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var tablePrefix = configuration["Database:TablePrefix"] ?? "123";

        // Database Settings for table prefix
        var databaseSettings = new DatabaseSettings { TablePrefix = tablePrefix };
        services.AddSingleton(databaseSettings);

        // Database - EF Core DbContext
        services.AddDbContext<ApplicationDbContext>((provider, options) =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // Identity with Argon2id
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = false;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // Replace default password hasher with Argon2id
        services.AddScoped<IPasswordHasher<ApplicationUser>, Argon2idPasswordHasher<ApplicationUser>>();

        // Redis
        var redisConnection = configuration.GetConnectionString("RedisConnection") ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnection));
        services.AddScoped<ICacheService, RedisCacheService>();

        // HttpClient for external APIs
        services.AddHttpClient();

        // Repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Services
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IAiService, GeminiAiService>();
        services.AddScoped<INotificationService, NotificationService>();

        // SignalR
        services.AddSignalR();
        services.AddSingleton<IUserIdProvider, MemberIdUserIdProvider>();

        // Hangfire - uses separate connection string
        var hangfireConnection = configuration.GetConnectionString("HangfireConnection")
            ?? configuration.GetConnectionString("DefaultConnection");
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(hangfireConnection, new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true
            }));

        services.AddHangfireServer();

        // Background Jobs
        services.AddScoped<BookingCleanupJob>();
        services.AddScoped<DailySummaryJob>();
        services.AddScoped<EloUpdateJob>();

        return services;
    }
}
