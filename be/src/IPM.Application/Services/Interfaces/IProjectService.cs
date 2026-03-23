using IPM.Application.DTOs.Common;
using IPM.Application.DTOs.Project;

namespace IPM.Application.Services.Interfaces;

public interface IProjectService
{
    Task<ApiResponse<List<ProjectDto>>> GetAllProjectsAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<List<ProjectSummaryDto>>> GetMyProjectsAsync(Guid memberId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ProjectDto>> GetProjectByIdAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ProjectDto>> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<ProjectDto>> UpdateProjectAsync(Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
