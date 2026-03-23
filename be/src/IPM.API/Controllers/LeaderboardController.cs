using IPM.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IPM.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaderboardController : ControllerBase
{
    private readonly ILeaderboardService _leaderboardService;

    public LeaderboardController(ILeaderboardService leaderboardService)
    {
        _leaderboardService = leaderboardService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,ProjectManager,Client,Subcontractor")]
    public async Task<IActionResult> GetTopSubcontractors([FromQuery] int count = 10, CancellationToken cancellationToken = default)
    {
        var result = await _leaderboardService.GetTopSubcontractorsAsync(count, cancellationToken);
        return Ok(result);
    }

    [HttpGet("my-rank")]
    [Authorize]
    public async Task<IActionResult> GetMyRank(CancellationToken cancellationToken)
    {
        var memberId = GetCurrentMemberId();
        var result = await _leaderboardService.GetMemberRankAsync(memberId, cancellationToken);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }

    [HttpPost("refresh")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RefreshLeaderboard(CancellationToken cancellationToken)
    {
        await _leaderboardService.RefreshLeaderboardAsync(cancellationToken);
        return Ok(new { success = true, message = "Leaderboard refreshed successfully" });
    }

    private Guid GetCurrentMemberId()
    {
        var memberIdClaim = User.Claims.FirstOrDefault(c => c.Type == "memberId")?.Value;
        return Guid.TryParse(memberIdClaim, out var memberId) ? memberId : Guid.Empty;
    }
}
