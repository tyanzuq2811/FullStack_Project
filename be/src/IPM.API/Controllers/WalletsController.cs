using IPM.Application.DTOs.Wallet;
using IPM.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IPM.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WalletsController : ControllerBase
{
    private readonly IWalletService _walletService;

    public WalletsController(IWalletService walletService)
    {
        _walletService = walletService;
    }

    [HttpGet("me")]
    [Authorize(Roles = "Admin,Accountant,ProjectManager,Client,Subcontractor")]
    public async Task<IActionResult> GetWalletSummary(CancellationToken cancellationToken)
    {
        var memberId = GetCurrentMemberId();
        var result = await _walletService.GetWalletSummaryAsync(memberId, cancellationToken);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }

    [HttpGet("transactions")]
    [Authorize(Roles = "Admin,Accountant,ProjectManager,Client,Subcontractor")]
    public async Task<IActionResult> GetTransactionHistory(
        [FromQuery] WalletHistoryRequest request,
        CancellationToken cancellationToken)
    {
        var memberId = GetCurrentMemberId();
        var result = await _walletService.GetTransactionHistoryAsync(memberId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("deposit")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> RequestDeposit([FromBody] DepositRequest request, CancellationToken cancellationToken)
    {
        var memberId = GetCurrentMemberId();
        var result = await _walletService.RequestDepositAsync(memberId, request, cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("approve/{id}")]
    [Authorize(Roles = "Accountant")]
    public async Task<IActionResult> ApproveDeposit(Guid id, [FromBody] bool approved, [FromQuery] string? notes, CancellationToken cancellationToken)
    {
        var adminId = GetCurrentMemberId();
        var request = new ApproveDepositRequest(id, approved, notes);
        var result = await _walletService.ApproveDepositAsync(adminId, request, cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("transfer")]
    [Authorize(Roles = "Accountant")]
    public async Task<IActionResult> Transfer([FromBody] TransferRequest request, CancellationToken cancellationToken)
    {
        var memberId = GetCurrentMemberId();
        var result = await _walletService.TransferAsync(memberId, request, cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("pending")]
    [Authorize(Roles = "Accountant")]
    public async Task<IActionResult> GetPendingDeposits(CancellationToken cancellationToken)
    {
        var result = await _walletService.GetPendingDepositsAsync(cancellationToken);
        return Ok(result);
    }

    private Guid GetCurrentMemberId()
    {
        var memberIdClaim = User.Claims.FirstOrDefault(c => c.Type == "memberId")?.Value;
        return Guid.TryParse(memberIdClaim, out var memberId) ? memberId : Guid.Empty;
    }
}
