using IPM.Application.DTOs.News;
using IPM.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IPM.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NewsController : ControllerBase
{
    private readonly INewsService _newsService;

    public NewsController(INewsService newsService)
    {
        _newsService = newsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllNews(CancellationToken cancellationToken)
    {
        var result = await _newsService.GetAllNewsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("pinned")]
    public async Task<IActionResult> GetPinnedNews(CancellationToken cancellationToken)
    {
        var result = await _newsService.GetPinnedNewsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetNews(int id, CancellationToken cancellationToken)
    {
        var result = await _newsService.GetNewsByIdAsync(id, cancellationToken);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateNews([FromBody] CreateNewsRequest request, CancellationToken cancellationToken)
    {
        var result = await _newsService.CreateNewsAsync(request, cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateNews(int id, [FromBody] UpdateNewsRequest request, CancellationToken cancellationToken)
    {
        var result = await _newsService.UpdateNewsAsync(id, request, cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteNews(int id, CancellationToken cancellationToken)
    {
        var result = await _newsService.DeleteNewsAsync(id, cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
}
