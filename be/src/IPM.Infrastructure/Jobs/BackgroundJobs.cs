using IPM.Domain.Entities;
using IPM.Domain.Enums;
using IPM.Domain.Interfaces;
using IPM.Infrastructure.Data;
using IPM.Infrastructure.Hubs;
using IPM.Application.Contracts.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IPM.Infrastructure.Jobs;

public class BookingCleanupJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BookingCleanupJob> _logger;

    public BookingCleanupJob(IServiceProvider serviceProvider, ILogger<BookingCleanupJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task CleanupExpiredPendingBookingsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub, INotificationClient>>();

        var cutoffTime = DateTime.UtcNow.AddMinutes(-15);

        var expiredBookings = await context.Bookings
            .Where(b => b.Status == BookingStatus.PendingPayment && b.CreatedAt < cutoffTime)
            .ToListAsync();

        if (expiredBookings.Any())
        {
            foreach (var booking in expiredBookings)
            {
                booking.Status = BookingStatus.Cancelled;
                _logger.LogInformation("Auto-cancelled expired booking {BookingId} for resource {ResourceId}", booking.Id, booking.ResourceId);

                // Notify clients about freed slot
                await hubContext.Clients.Group("booking_updates")
                    .BookingStatusChanged(new BookingStatusChangedDto(
                        booking.Id,
                        booking.ResourceId,
                        booking.StartTime,
                        booking.EndTime,
                        (int)BookingStatus.Cancelled));
            }

            await context.SaveChangesAsync();
            _logger.LogInformation("Cleaned up {Count} expired pending bookings", expiredBookings.Count);
        }
    }
}

public class DailySummaryJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailySummaryJob> _logger;

    public DailySummaryJob(IServiceProvider serviceProvider, ILogger<DailySummaryJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task GenerateDailySummaryAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

        var today = DateTime.UtcNow.Date;

        // Calculate total disbursed amount today
        var totalDisbursed = await context.WalletTransactions
            .Where(t => t.CreatedAt.Date == today && t.Status == TransactionStatus.Success && t.TransType == TransactionType.Debit)
            .SumAsync(t => t.Amount);

        // Update ELO leaderboard in Redis
        var subcontractors = await context.Members
            .Where(m => m.IsActive)
            .OrderByDescending(m => m.RankELO)
            .Take(100)
            .Select(m => new { m.Id, m.FullName, m.RankELO })
            .ToListAsync();

        foreach (var sub in subcontractors)
        {
            await cacheService.AddToSortedSetAsync("leaderboard:elo", sub.Id.ToString(), sub.RankELO);
        }

        _logger.LogInformation("Daily summary generated. Total disbursed: {Amount}. Updated {Count} contractors in leaderboard.",
            totalDisbursed, subcontractors.Count);
    }
}

public class EloUpdateJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EloUpdateJob> _logger;

    public EloUpdateJob(IServiceProvider serviceProvider, ILogger<EloUpdateJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task UpdateSubcontractorEloAsync(long taskId, Guid subcontractorId, bool onTime, int anomalyCount)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

        var member = await context.Members.FindAsync(subcontractorId);
        if (member == null) return;

        // ELO calculation
        double eloChange = 0;

        // Base change based on on-time completion
        if (onTime)
        {
            eloChange += 25; // Bonus for on-time
        }
        else
        {
            eloChange -= 15; // Penalty for late
        }

        // Penalty for anomalies detected by AI
        eloChange -= anomalyCount * 5;

        // Update ELO with bounds
        member.RankELO = Math.Max(0, Math.Min(3000, member.RankELO + eloChange));

        await context.SaveChangesAsync();

        // Update Redis leaderboard
        await cacheService.AddToSortedSetAsync("leaderboard:elo", subcontractorId.ToString(), member.RankELO);

        _logger.LogInformation("Updated ELO for subcontractor {SubcontractorId}: {Change:+0.##} (new: {NewElo})",
            subcontractorId, eloChange, member.RankELO);
    }
}
