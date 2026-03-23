using System.Text;
using System.Data;
using IPM.Domain.Entities;
using Hangfire;
using IPM.API.Middleware;
using IPM.Application;
using IPM.Infrastructure;
using IPM.Infrastructure.Data;
using IPM.Infrastructure.Hubs;
using IPM.Infrastructure.Jobs;
using IPM.Domain.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "IPM Pro API",
        Version = "v1",
        Description = "Interior Project Manager Pro Edition API"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' followed by space and your JWT token"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add Application and Infrastructure layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]!)),
        ClockSkew = TimeSpan.Zero,
        RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
    };

    // For SignalR
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/api/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                builder.Configuration["App:FrontendUrl"] ?? "http://localhost:5173",
                "http://localhost",
                "http://localhost:80"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        await EnsureDatabaseReadyAsync(context, logger);

        // Seed data
        await SeedData.SeedAsync(context, userManager, roleManager);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI();

app.UseGlobalExceptionMiddleware();

// Only use HTTPS redirection in Development with HTTPS configured
// app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

// SignalR Hub
app.MapHub<NotificationHub>("/api/hubs/notifications");

// Hangfire Dashboard (secured)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

// Configure recurring jobs
RecurringJob.AddOrUpdate<BookingCleanupJob>(
    "cleanup-expired-bookings",
    job => job.CleanupExpiredPendingBookingsAsync(),
    "*/5 * * * *"); // Every 5 minutes

RecurringJob.AddOrUpdate<DailySummaryJob>(
    "daily-summary",
    job => job.GenerateDailySummaryAsync(),
    "59 23 * * *"); // At 23:59 every day

app.MapControllers();

app.Run();

static async Task EnsureDatabaseReadyAsync(ApplicationDbContext context, ILogger logger)
{
    var existingTables = await GetExistingTableNamesAsync(context);
    var hasMigrationHistory = existingTables.Contains("__EFMigrationsHistory");

    var appTableNames = context.Model.GetEntityTypes()
        .Select(entity => entity.GetTableName())
        .Where(tableName => !string.IsNullOrWhiteSpace(tableName))
        .Select(tableName => tableName!)
        .Where(tableName => !tableName.StartsWith("AspNet", StringComparison.OrdinalIgnoreCase))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    var hasAnyAppTable = appTableNames.Any(tableName => existingTables.Contains(tableName));

    if (hasMigrationHistory)
    {
        await context.Database.MigrateAsync();
        logger.LogInformation("Database bootstrap mode: migrate (migration history found).");
        return;
    }

    if (!hasAnyAppTable)
    {
        await context.Database.MigrateAsync();
        logger.LogInformation("Database bootstrap mode: migrate (new database without app tables).");
        return;
    }

    if (await TryRunSafeLegacyUpgradeAsync(context, existingTables, logger))
    {
        await context.Database.MigrateAsync();
        logger.LogInformation("Database bootstrap mode: safe one-time legacy upgrade to migrations completed.");
        return;
    }

    // Legacy fallback path: schema exists but does not pass upgrade guards.
    await context.Database.EnsureCreatedAsync();
    logger.LogWarning("Database bootstrap mode: legacy ensure-created (schema guard failed, skipped auto-upgrade).");
}

static async Task<bool> TryRunSafeLegacyUpgradeAsync(ApplicationDbContext context, HashSet<string> existingTables, ILogger logger)
{
    // One-time upgrade path is only enabled for SQL Server where migrations are authored.
    if (!context.Database.IsSqlServer())
    {
        logger.LogWarning("Safe legacy upgrade skipped: unsupported provider for auto-baseline.");
        return false;
    }

    var tasksTable = context.Model.FindEntityType(typeof(ProjectTask))?.GetTableName();
    var projectsTable = context.Model.FindEntityType(typeof(Project))?.GetTableName();
    var mediaTable = context.Model.FindEntityType(typeof(MediaFile))?.GetTableName();
    var membersTable = context.Model.FindEntityType(typeof(Member))?.GetTableName();

    if (string.IsNullOrWhiteSpace(tasksTable) ||
        string.IsNullOrWhiteSpace(projectsTable) ||
        string.IsNullOrWhiteSpace(mediaTable) ||
        string.IsNullOrWhiteSpace(membersTable))
    {
        logger.LogWarning("Safe legacy upgrade skipped: unable to resolve critical table names from model.");
        return false;
    }

    var requiredTables = new[] { tasksTable, projectsTable, mediaTable, membersTable };
    if (requiredTables.Any(tableName => !existingTables.Contains(tableName)))
    {
        logger.LogWarning("Safe legacy upgrade skipped: not all critical legacy tables are present.");
        return false;
    }

    var taskColumns = await GetTableColumnsAsync(context, tasksTable);
    var projectColumns = await GetTableColumnsAsync(context, projectsTable);
    var mediaColumns = await GetTableColumnsAsync(context, mediaTable);
    var memberColumns = await GetTableColumnsAsync(context, membersTable);

    var taskColumnsOk = HasColumns(taskColumns, "Id", "ProjectId", "Name", "Status", "ProgressPct");
    var projectColumnsOk = HasColumns(projectColumns, "Id", "ManagerId", "ClientId", "Name");
    var mediaColumnsOk = HasColumns(mediaColumns, "Id", "ReferenceId", "FileUrl", "Type", "CreatedAt");
    var memberColumnsOk = HasColumns(memberColumns, "Id", "FullName", "Email");

    if (!taskColumnsOk || !projectColumnsOk || !mediaColumnsOk || !memberColumnsOk)
    {
        logger.LogWarning("Safe legacy upgrade skipped: legacy schema does not match expected baseline shape.");
        return false;
    }

    const string baselineMigrationId = "20260321193224_InitialCreate";
    const string baselineProductVersion = "8.0.0";

    await using var tx = await context.Database.BeginTransactionAsync();
    try
    {
        await context.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'__EFMigrationsHistory', N'U') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory]
    (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END");

        await context.Database.ExecuteSqlInterpolatedAsync($@"
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = {baselineMigrationId})
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ({baselineMigrationId}, {baselineProductVersion});
END");

        await tx.CommitAsync();
        logger.LogInformation("Safe legacy upgrade: baseline migration history inserted.");
        return true;
    }
    catch (Exception ex)
    {
        await tx.RollbackAsync();
        logger.LogWarning(ex, "Safe legacy upgrade failed while inserting baseline history.");
        return false;
    }
}

static bool HasColumns(HashSet<string> existingColumns, params string[] requiredColumns)
{
    return requiredColumns.All(existingColumns.Contains);
}

static async Task<HashSet<string>> GetExistingTableNamesAsync(ApplicationDbContext context)
{
    var tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var connection = context.Database.GetDbConnection();

    if (connection.State != ConnectionState.Open)
    {
        await connection.OpenAsync();
    }

    try
    {
        var schema = connection.GetSchema("Tables");
        foreach (DataRow row in schema.Rows)
        {
            var tableName = row["TABLE_NAME"]?.ToString();
            if (!string.IsNullOrWhiteSpace(tableName))
            {
                tableNames.Add(tableName);
            }
        }
    }
    finally
    {
        await connection.CloseAsync();
    }

    return tableNames;
}

static async Task<HashSet<string>> GetTableColumnsAsync(ApplicationDbContext context, string tableName)
{
    var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var connection = context.Database.GetDbConnection();

    if (connection.State != ConnectionState.Open)
    {
        await connection.OpenAsync();
    }

    try
    {
        var restrictions = new string?[] { null, null, tableName, null };
        var schema = connection.GetSchema("Columns", restrictions);
        foreach (DataRow row in schema.Rows)
        {
            var columnName = row["COLUMN_NAME"]?.ToString();
            if (!string.IsNullOrWhiteSpace(columnName))
            {
                columns.Add(columnName);
            }
        }
    }
    finally
    {
        await connection.CloseAsync();
    }

    return columns;
}

// Hangfire Authorization Filter
public class HangfireAuthorizationFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext context)
    {
        var remoteIp = context.Request.RemoteIpAddress;
        var localIp = context.Request.LocalIpAddress;

        if (string.IsNullOrWhiteSpace(remoteIp))
        {
            return false;
        }

        // Allow dashboard access only from local machine/container network.
        return remoteIp == "127.0.0.1" ||
               remoteIp == "::1" ||
               (!string.IsNullOrWhiteSpace(localIp) && remoteIp == localIp);
    }
}
