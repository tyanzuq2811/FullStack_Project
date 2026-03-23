using IPM.Application.DTOs.Common;
using IPM.Application.DTOs.News;
using IPM.Application.Services.Interfaces;
using IPM.Domain.Entities;
using IPM.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IPM.Application.Services;

public class NewsService : INewsService
{
    private readonly IRepository<News> _newsRepository;
    private readonly ICacheService _cacheService;
    private readonly IUnitOfWork _unitOfWork;

    private const string PinnedNewsCacheKey = "news:pinned";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(30);

    public NewsService(
        IRepository<News> newsRepository,
        ICacheService cacheService,
        IUnitOfWork unitOfWork)
    {
        _newsRepository = newsRepository;
        _cacheService = cacheService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<List<NewsDto>>> GetAllNewsAsync(CancellationToken cancellationToken = default)
    {
        var news = await _newsRepository.Query()
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.CreatedDate)
            .ToListAsync(cancellationToken);

        var dtos = news.Select(MapToDto).ToList();
        return ApiResponse<List<NewsDto>>.SuccessResponse(dtos);
    }

    public async Task<ApiResponse<List<NewsDto>>> GetPinnedNewsAsync(CancellationToken cancellationToken = default)
    {
        // Try to get from cache first
        var cached = await _cacheService.GetAsync<List<NewsDto>>(PinnedNewsCacheKey, cancellationToken);
        if (cached != null)
        {
            return ApiResponse<List<NewsDto>>.SuccessResponse(cached);
        }

        // Get from database
        var news = await _newsRepository.Query()
            .Where(n => n.IsPinned)
            .OrderByDescending(n => n.CreatedDate)
            .ToListAsync(cancellationToken);

        var dtos = news.Select(MapToDto).ToList();

        // Cache the result
        await _cacheService.SetAsync(PinnedNewsCacheKey, dtos, CacheExpiration, cancellationToken);

        return ApiResponse<List<NewsDto>>.SuccessResponse(dtos);
    }

    public async Task<ApiResponse<NewsDto>> GetNewsByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var news = await _newsRepository.GetByIdAsync(id, cancellationToken);
        if (news == null)
        {
            return ApiResponse<NewsDto>.FailResponse("Thông báo không tồn tại");
        }

        return ApiResponse<NewsDto>.SuccessResponse(MapToDto(news));
    }

    public async Task<ApiResponse<NewsDto>> CreateNewsAsync(CreateNewsRequest request, CancellationToken cancellationToken = default)
    {
        var news = new News
        {
            Title = request.Title,
            Content = request.Content,
            IsPinned = request.IsPinned
        };

        await _newsRepository.AddAsync(news, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate cache if pinned
        if (request.IsPinned)
        {
            await _cacheService.RemoveAsync(PinnedNewsCacheKey, cancellationToken);
        }

        return ApiResponse<NewsDto>.SuccessResponse(MapToDto(news), "Tạo thông báo thành công");
    }

    public async Task<ApiResponse<NewsDto>> UpdateNewsAsync(int id, UpdateNewsRequest request, CancellationToken cancellationToken = default)
    {
        var news = await _newsRepository.GetByIdAsync(id, cancellationToken);
        if (news == null)
        {
            return ApiResponse<NewsDto>.FailResponse("Thông báo không tồn tại");
        }

        var wasPinned = news.IsPinned;

        if (request.Title != null)
            news.Title = request.Title;

        if (request.Content != null)
            news.Content = request.Content;

        if (request.IsPinned.HasValue)
            news.IsPinned = request.IsPinned.Value;

        _newsRepository.Update(news);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate cache if pinned status changed
        if (wasPinned || news.IsPinned)
        {
            await _cacheService.RemoveAsync(PinnedNewsCacheKey, cancellationToken);
        }

        return ApiResponse<NewsDto>.SuccessResponse(MapToDto(news), "Cập nhật thông báo thành công");
    }

    public async Task<ApiResponse<bool>> DeleteNewsAsync(int id, CancellationToken cancellationToken = default)
    {
        var news = await _newsRepository.GetByIdAsync(id, cancellationToken);
        if (news == null)
        {
            return ApiResponse<bool>.FailResponse("Thông báo không tồn tại");
        }

        var wasPinned = news.IsPinned;

        _newsRepository.Remove(news);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate cache if was pinned
        if (wasPinned)
        {
            await _cacheService.RemoveAsync(PinnedNewsCacheKey, cancellationToken);
        }

        return ApiResponse<bool>.SuccessResponse(true, "Xóa thông báo thành công");
    }

    private static NewsDto MapToDto(News news)
    {
        return new NewsDto(
            news.Id,
            news.Title,
            news.Content,
            news.IsPinned,
            news.CreatedDate
        );
    }
}
