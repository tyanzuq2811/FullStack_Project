using IPM.Application.DTOs.Project;
using IPM.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IPM.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<IActionResult> GetAllProjects(CancellationToken cancellationToken)
    {
        var result = await _projectService.GetAllProjectsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyProjects(CancellationToken cancellationToken)
    {
        var memberId = GetCurrentMemberId();
        var result = await _projectService.GetMyProjectsAsync(memberId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProject(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAdminOrAccountant())
        {
            var memberId = GetCurrentMemberId();
            var accessibleProjects = await _projectService.GetMyProjectsAsync(memberId, cancellationToken);
            if (!accessibleProjects.Success || accessibleProjects.Data?.Any(p => p.Id == id) != true)
            {
                return Forbid();
            }
        }

        var result = await _projectService.GetProjectByIdAsync(id, cancellationToken);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "ProjectManager")]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var result = await _projectService.CreateProjectAsync(request, cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "ProjectManager")]
    public async Task<IActionResult> UpdateProject(Guid id, [FromBody] UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        var result = await _projectService.UpdateProjectAsync(id, request, cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "ProjectManager")]
    public async Task<IActionResult> DeleteProject(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectService.DeleteProjectAsync(id, cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    private Guid GetCurrentMemberId()
    {
        var memberIdClaim = User.Claims.FirstOrDefault(c => c.Type == "memberId")?.Value;
        return Guid.TryParse(memberIdClaim, out var memberId) ? memberId : Guid.Empty;
    }

    private bool IsAdminOrAccountant()
    {
        return User.IsInRole("Admin") || User.IsInRole("Accountant");
    }
}
