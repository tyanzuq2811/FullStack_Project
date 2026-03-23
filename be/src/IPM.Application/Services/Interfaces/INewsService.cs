using IPM.Application.DTOs.Common;
using IPM.Application.DTOs.News;

namespace IPM.Application.Services.Interfaces;

public interface INewsService
{
    Task<ApiResponse<List<NewsDto>>> GetAllNewsAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<List<NewsDto>>> GetPinnedNewsAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<NewsDto>> GetNewsByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResponse<NewsDto>> CreateNewsAsync(CreateNewsRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<NewsDto>> UpdateNewsAsync(int id, UpdateNewsRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteNewsAsync(int id, CancellationToken cancellationToken = default);
}
