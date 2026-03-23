using IPM.Application.DTOs.Common;
using IPM.Application.DTOs.Resource;

namespace IPM.Application.Services.Interfaces;

public interface IResourceService
{
    Task<ApiResponse<List<ResourceDto>>> GetAllResourcesAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<ResourceDto>> GetResourceByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResponse<ResourceDto>> CreateResourceAsync(CreateResourceRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<ResourceDto>> UpdateResourceAsync(int id, UpdateResourceRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteResourceAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ToggleResourceStatusAsync(int id, CancellationToken cancellationToken = default);
}
