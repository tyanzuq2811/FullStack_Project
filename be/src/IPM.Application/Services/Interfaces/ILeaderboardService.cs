using IPM.Application.DTOs.Common;

namespace IPM.Application.Services.Interfaces;

public interface ILeaderboardService
{
    Task<ApiResponse<List<LeaderboardEntry>>> GetTopSubcontractorsAsync(int count = 10, CancellationToken cancellationToken = default);
    Task<ApiResponse<LeaderboardEntry>> GetMemberRankAsync(Guid memberId, CancellationToken cancellationToken = default);
    Task RefreshLeaderboardAsync(CancellationToken cancellationToken = default);
}
