using IPM.Application.DTOs.Common;
using IPM.Application.Services.Interfaces;
using IPM.Domain.Entities;
using IPM.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IPM.Application.Services;

public class LeaderboardService : ILeaderboardService
{
    private readonly IRepository<Member> _memberRepository;
    private readonly ICacheService _cacheService;

    private const string LeaderboardKey = "leaderboard:elo";

    public LeaderboardService(
        IRepository<Member> memberRepository,
        ICacheService cacheService)
    {
        _memberRepository = memberRepository;
        _cacheService = cacheService;
    }

    public async Task<ApiResponse<List<LeaderboardEntry>>> GetTopSubcontractorsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        // Try to get from Redis Sorted Set first (super fast)
        var cachedEntries = await _cacheService.GetSortedSetRangeAsync(LeaderboardKey, 0, count - 1, descending: true, cancellationToken);

        if (cachedEntries.Any())
        {
            // Get member names from database
            var memberIds = cachedEntries.Select(e => Guid.TryParse(e.member, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).ToList();
            var members = await _memberRepository.Query()
                .Where(m => memberIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, m => m.FullName, cancellationToken);

            var entries = cachedEntries
                .Where(e => Guid.TryParse(e.member, out _))
                .Select((e, index) =>
                {
                    var memberId = Guid.Parse(e.member);
                    return new LeaderboardEntry(
                        index + 1,
                        memberId,
                        members.GetValueOrDefault(memberId, "Unknown"),
                        e.score
                    );
                })
                .ToList();

            return ApiResponse<List<LeaderboardEntry>>.SuccessResponse(entries);
        }

        // Fallback to database
        var topMembers = await _memberRepository.Query()
            .Where(m => m.IsActive)
            .OrderByDescending(m => m.RankELO)
            .Take(count)
            .Select(m => new { m.Id, m.FullName, m.RankELO })
            .ToListAsync(cancellationToken);

        var leaderboard = topMembers
            .Select((m, index) => new LeaderboardEntry(index + 1, m.Id, m.FullName, m.RankELO))
            .ToList();

        return ApiResponse<List<LeaderboardEntry>>.SuccessResponse(leaderboard);
    }

    public async Task<ApiResponse<LeaderboardEntry>> GetMemberRankAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        // Try to get score from Redis
        var score = await _cacheService.GetSortedSetScoreAsync(LeaderboardKey, memberId.ToString(), cancellationToken);

        if (score.HasValue)
        {
            // Calculate rank by counting members with higher scores
            var allScores = await _cacheService.GetSortedSetRangeAsync(LeaderboardKey, 0, -1, descending: true, cancellationToken);
            var rank = allScores.TakeWhile(e => e.score > score.Value).Count() + 1;

            var member = await _memberRepository.GetByIdAsync(memberId, cancellationToken);
            if (member == null)
            {
                return ApiResponse<LeaderboardEntry>.FailResponse("Thành viên không tồn tại");
            }

            return ApiResponse<LeaderboardEntry>.SuccessResponse(new LeaderboardEntry(
                rank,
                memberId,
                member.FullName,
                score.Value
            ));
        }

        // Fallback to database
        var memberFromDb = await _memberRepository.GetByIdAsync(memberId, cancellationToken);
        if (memberFromDb == null)
        {
            return ApiResponse<LeaderboardEntry>.FailResponse("Thành viên không tồn tại");
        }

        var higherRanked = await _memberRepository.Query()
            .Where(m => m.IsActive && m.RankELO > memberFromDb.RankELO)
            .CountAsync(cancellationToken);

        return ApiResponse<LeaderboardEntry>.SuccessResponse(new LeaderboardEntry(
            higherRanked + 1,
            memberId,
            memberFromDb.FullName,
            memberFromDb.RankELO
        ));
    }

    public async Task RefreshLeaderboardAsync(CancellationToken cancellationToken = default)
    {
        var members = await _memberRepository.Query()
            .Where(m => m.IsActive)
            .OrderByDescending(m => m.RankELO)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var member in members)
        {
            await _cacheService.AddToSortedSetAsync(LeaderboardKey, member.Id.ToString(), member.RankELO, cancellationToken);
        }
    }
}
