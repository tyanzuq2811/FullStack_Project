using IPM.Application.DTOs.AI;
using IPM.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IPM.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IAiAnalysisService _aiAnalysisService;

    public AiController(IAiAnalysisService aiAnalysisService)
    {
        _aiAnalysisService = aiAnalysisService;
    }

    /// <summary>
    /// Analyze construction progress from an image using Google Gemini AI
    /// </summary>
    [HttpPost("analyze-progress/{taskId}")]
    [Authorize(Roles = "Subcontractor,ProjectManager")]
    public async Task<IActionResult> AnalyzeProgress(long taskId, [FromBody] AnalyzeProgressRequest request, CancellationToken cancellationToken)
    {
        var memberId = GetCurrentMemberId();
        var result = await _aiAnalysisService.AnalyzeProgressAsync(
            taskId,
            request,
            memberId,
            User.IsInRole("ProjectManager"),
            User.IsInRole("Subcontractor"),
            cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Extract invoice data using OCR (Google Gemini AI)
    /// </summary>
    [HttpPost("analyze-invoice")]
    [Authorize(Roles = "ProjectManager,Accountant")]
    public async Task<IActionResult> AnalyzeInvoice([FromBody] AnalyzeInvoiceRequest request, CancellationToken cancellationToken)
    {
        var memberId = GetCurrentMemberId();
        var result = await _aiAnalysisService.AnalyzeInvoiceAsync(
            request,
            memberId,
            User.IsInRole("Accountant"),
            User.IsInRole("ProjectManager"),
            cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    private Guid GetCurrentMemberId()
    {
        var memberIdClaim = User.Claims.FirstOrDefault(c => c.Type == "memberId")?.Value;
        return Guid.TryParse(memberIdClaim, out var memberId) ? memberId : Guid.Empty;
    }
}
