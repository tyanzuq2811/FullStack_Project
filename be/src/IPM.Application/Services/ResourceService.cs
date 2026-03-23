using IPM.Application.DTOs.Common;
using IPM.Application.DTOs.Resource;
using IPM.Application.Services.Interfaces;
using IPM.Domain.Entities;
using IPM.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IPM.Application.Services;

public class ResourceService : IResourceService
{
    private readonly IRepository<Resource> _resourceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ResourceService(
        IRepository<Resource> resourceRepository,
        IUnitOfWork unitOfWork)
    {
        _resourceRepository = resourceRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<List<ResourceDto>>> GetAllResourcesAsync(CancellationToken cancellationToken = default)
    {
        var resources = await _resourceRepository.Query()
            .Include(r => r.Bookings)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        var dtos = resources.Select(r => new ResourceDto(
            r.Id,
            r.Name,
            r.Description,
            r.HourlyRate,
            r.IsActive,
            r.Bookings.Count
        )).ToList();

        return ApiResponse<List<ResourceDto>>.SuccessResponse(dtos);
    }

    public async Task<ApiResponse<ResourceDto>> GetResourceByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var resource = await _resourceRepository.Query()
            .Include(r => r.Bookings)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (resource == null)
        {
            return ApiResponse<ResourceDto>.FailResponse("Tài nguyên không tồn tại");
        }

        var dto = new ResourceDto(
            resource.Id,
            resource.Name,
            resource.Description,
            resource.HourlyRate,
            resource.IsActive,
            resource.Bookings.Count
        );

        return ApiResponse<ResourceDto>.SuccessResponse(dto);
    }

    public async Task<ApiResponse<ResourceDto>> CreateResourceAsync(CreateResourceRequest request, CancellationToken cancellationToken = default)
    {
        // Check if name already exists
        var existing = await _resourceRepository.Query()
            .FirstOrDefaultAsync(r => r.Name == request.Name, cancellationToken);

        if (existing != null)
        {
            return ApiResponse<ResourceDto>.FailResponse("Tài nguyên với tên này đã tồn tại");
        }

        var resource = new Resource
        {
            Name = request.Name,
            Description = request.Description,
            HourlyRate = request.HourlyRate,
            IsActive = true
        };

        await _resourceRepository.AddAsync(resource, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new ResourceDto(
            resource.Id,
            resource.Name,
            resource.Description,
            resource.HourlyRate,
            resource.IsActive,
            0
        );

        return ApiResponse<ResourceDto>.SuccessResponse(dto, "Tạo tài nguyên thành công");
    }

    public async Task<ApiResponse<ResourceDto>> UpdateResourceAsync(int id, UpdateResourceRequest request, CancellationToken cancellationToken = default)
    {
        var resource = await _resourceRepository.Query()
            .Include(r => r.Bookings)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (resource == null)
        {
            return ApiResponse<ResourceDto>.FailResponse("Tài nguyên không tồn tại");
        }

        if (!string.IsNullOrEmpty(request.Name) && request.Name != resource.Name)
        {
            var existing = await _resourceRepository.Query()
                .FirstOrDefaultAsync(r => r.Name == request.Name && r.Id != id, cancellationToken);
            if (existing != null)
            {
                return ApiResponse<ResourceDto>.FailResponse("Tài nguyên với tên này đã tồn tại");
            }
            resource.Name = request.Name;
        }

        if (request.Description != null)
            resource.Description = request.Description;

        if (request.HourlyRate.HasValue)
            resource.HourlyRate = request.HourlyRate.Value;

        if (request.IsActive.HasValue)
            resource.IsActive = request.IsActive.Value;

        _resourceRepository.Update(resource);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new ResourceDto(
            resource.Id,
            resource.Name,
            resource.Description,
            resource.HourlyRate,
            resource.IsActive,
            resource.Bookings.Count
        );

        return ApiResponse<ResourceDto>.SuccessResponse(dto, "Cập nhật tài nguyên thành công");
    }

    public async Task<ApiResponse<bool>> DeleteResourceAsync(int id, CancellationToken cancellationToken = default)
    {
        var resource = await _resourceRepository.Query()
            .Include(r => r.Bookings)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (resource == null)
        {
            return ApiResponse<bool>.FailResponse("Tài nguyên không tồn tại");
        }

        if (resource.Bookings.Any())
        {
            // Soft delete - just deactivate
            resource.IsActive = false;
            _resourceRepository.Update(resource);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<bool>.SuccessResponse(true, "Đã vô hiệu hóa tài nguyên (có booking liên quan)");
        }

        _resourceRepository.Remove(resource);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "Đã xóa tài nguyên");
    }

    public async Task<ApiResponse<bool>> ToggleResourceStatusAsync(int id, CancellationToken cancellationToken = default)
    {
        var resource = await _resourceRepository.GetByIdAsync(id, cancellationToken);
        if (resource == null)
        {
            return ApiResponse<bool>.FailResponse("Tài nguyên không tồn tại");
        }

        resource.IsActive = !resource.IsActive;
        _resourceRepository.Update(resource);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var status = resource.IsActive ? "kích hoạt" : "vô hiệu hóa";
        return ApiResponse<bool>.SuccessResponse(true, $"Đã {status} tài nguyên");
    }
}
