using IPM.Application.DTOs.Resource;
using IPM.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IPM.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ResourcesController : ControllerBase
{
    private readonly IResourceService _resourceService;

    public ResourcesController(IResourceService resourceService)
    {
        _resourceService = resourceService;
    }

    /// <summary>
    /// Get all resources/categories
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllResources(CancellationToken cancellationToken)
    {
        var result = await _resourceService.GetAllResourcesAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get resource by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetResource(int id, CancellationToken cancellationToken)
    {
        var result = await _resourceService.GetResourceByIdAsync(id, cancellationToken);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// Create new resource (Admin only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateResource([FromBody] CreateResourceRequest request, CancellationToken cancellationToken)
    {
        var result = await _resourceService.CreateResourceAsync(request, cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Update resource (Admin only)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateResource(int id, [FromBody] UpdateResourceRequest request, CancellationToken cancellationToken)
    {
        var result = await _resourceService.UpdateResourceAsync(id, request, cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Delete resource (Admin only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteResource(int id, CancellationToken cancellationToken)
    {
        var result = await _resourceService.DeleteResourceAsync(id, cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Toggle resource status (Admin only)
    /// </summary>
    [HttpPost("{id}/toggle-status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ToggleResourceStatus(int id, CancellationToken cancellationToken)
    {
        var result = await _resourceService.ToggleResourceStatusAsync(id, cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
}
