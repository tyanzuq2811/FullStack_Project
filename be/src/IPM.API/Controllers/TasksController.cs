using IPM.Application.DTOs.Task;
using IPM.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IPM.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly IProjectService _projectService;

    public TasksController(ITaskService taskService, IProjectService projectService)
    {
        _taskService = taskService;
        _projectService = projectService;
    }

    [HttpGet("project/{projectId}")]
    [Authorize(Roles = "Admin,Accountant,ProjectManager,Subcontractor,Client")]
    public async Task<IActionResult> GetTasksByProject(Guid projectId, CancellationToken cancellationToken)
    {
        if (!await CanAccessProjectAsync(projectId, cancellationToken))
        {
            return Forbid();
        }

        var result = await _taskService.GetTasksByProjectAsync(projectId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("my")]
    [Authorize(Roles = "Subcontractor")]
    public async Task<IActionResult> GetMyTasks(CancellationToken cancellationToken)
    {
        var memberId = GetCurrentMemberId();
        var result = await _taskService.GetMyTasksAsync(memberId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Accountant,ProjectManager,Subcontractor,Client")]
    public async Task<IActionResult> GetTask(long id, CancellationToken cancellationToken)
    {
        var result = await _taskService.GetTaskByIdAsync(id, cancellationToken);
        if (!result.Success)
            return NotFound(result);

        if (result.Data == null || !await CanAccessProjectAsync(result.Data.ProjectId, cancellationToken))
        {
            return Forbid();
        }

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "ProjectManager")]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await _taskService.CreateTaskAsync(request, cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "ProjectManager")]
    public async Task<IActionResult> UpdateTask(long id, [FromBody] UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await _taskService.UpdateTaskAsync(id, request, cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "ProjectManager,Subcontractor")]
    public async Task<IActionResult> UpdateTaskStatus(long id, [FromBody] TaskStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        var memberId = GetCurrentMemberId();
        var result = await _taskService.UpdateTaskStatusAsync(id, memberId, request, cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("{id}/progress")]
    [Authorize(Roles = "Subcontractor")]
    public async Task<IActionResult> UpdateTaskProgress(long id, [FromBody] TaskProgressUpdateRequest request, CancellationToken cancellationToken)
    {
        var memberId = GetCurrentMemberId();
        var result = await _taskService.UpdateTaskProgressAsync(id, memberId, request, cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{id}/approve")]
    [Authorize(Roles = "ProjectManager")]
    public async Task<IActionResult> ApproveTask(long id, [FromBody] TaskApprovalRequest request, CancellationToken cancellationToken)
    {
        var managerId = GetCurrentMemberId();
        var result = await _taskService.ApproveTaskAsync(id, managerId, request, cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "ProjectManager")]
    public async Task<IActionResult> DeleteTask(long id, CancellationToken cancellationToken)
    {
        var result = await _taskService.DeleteTaskAsync(id, cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    private Guid GetCurrentMemberId()
    {
        var memberIdClaim = User.Claims.FirstOrDefault(c => c.Type == "memberId")?.Value;
        return Guid.TryParse(memberIdClaim, out var memberId) ? memberId : Guid.Empty;
    }

    private async Task<bool> CanAccessProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Admin") || User.IsInRole("Accountant"))
        {
            return true;
        }

        var memberId = GetCurrentMemberId();
        var accessibleProjects = await _projectService.GetMyProjectsAsync(memberId, cancellationToken);
        return accessibleProjects.Success && accessibleProjects.Data?.Any(p => p.Id == projectId) == true;
    }
}
