using IPM.Application.DTOs.Common;
using IPM.Application.DTOs.Task;

namespace IPM.Application.Services.Interfaces;

public interface ITaskService
{
    Task<ApiResponse<List<TaskDto>>> GetTasksByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<TaskDto>>> GetMyTasksAsync(Guid subcontractorId, CancellationToken cancellationToken = default);
    Task<ApiResponse<TaskDto>> GetTaskByIdAsync(long taskId, CancellationToken cancellationToken = default);
    Task<ApiResponse<TaskDto>> CreateTaskAsync(CreateTaskRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<TaskDto>> UpdateTaskAsync(long taskId, UpdateTaskRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<TaskDto>> UpdateTaskStatusAsync(long taskId, Guid memberId, TaskStatusUpdateRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<TaskDto>> UpdateTaskProgressAsync(long taskId, Guid memberId, TaskProgressUpdateRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<TaskDto>> ApproveTaskAsync(long taskId, Guid managerId, TaskApprovalRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteTaskAsync(long taskId, CancellationToken cancellationToken = default);
}
