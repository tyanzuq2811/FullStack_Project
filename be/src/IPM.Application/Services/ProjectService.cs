using IPM.Application.DTOs.Common;
using IPM.Application.DTOs.Project;
using IPM.Application.Services.Interfaces;
using IPM.Domain.Entities;
using IPM.Domain.Enums;
using IPM.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IPM.Application.Services;

public class ProjectService : IProjectService
{
    private const decimal MaxTotalBudget = 9999999999999999.99m;
    private readonly IRepository<Project> _projectRepository;
    private readonly IRepository<Member> _memberRepository;
    private readonly IRepository<ProjectTask> _taskRepository;
    private readonly IRepository<ProjectBudget> _budgetRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProjectService(
        IRepository<Project> projectRepository,
        IRepository<Member> memberRepository,
        IRepository<ProjectTask> taskRepository,
        IRepository<ProjectBudget> budgetRepository,
        IUnitOfWork unitOfWork)
    {
        _projectRepository = projectRepository;
        _memberRepository = memberRepository;
        _taskRepository = taskRepository;
        _budgetRepository = budgetRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<List<ProjectDto>>> GetAllProjectsAsync(CancellationToken cancellationToken = default)
    {
        var projects = await _projectRepository.Query()
            .Include(p => p.Client)
            .Include(p => p.Manager)
            .Include(p => p.Tasks)
            .Include(p => p.Budgets)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(cancellationToken);

        var dtos = projects.Select(MapToDto).ToList();
        return ApiResponse<List<ProjectDto>>.SuccessResponse(dtos);
    }

    public async Task<ApiResponse<List<ProjectSummaryDto>>> GetMyProjectsAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var projects = await _projectRepository.Query()
            .Include(p => p.Tasks)
            .Where(p => p.ClientId == memberId || p.ManagerId == memberId ||
                        p.Tasks.Any(t => t.SubcontractorId == memberId))
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(cancellationToken);

        var dtos = projects.Select(p =>
        {
            var completedTasks = p.Tasks.Count(t => t.Status == ProjectTaskStatus.Approved);
            var totalTasks = p.Tasks.Count;
            var progress = totalTasks > 0 ? (double)completedTasks / totalTasks * 100 : 0;
            var daysRemaining = p.TargetDate.HasValue ? (p.TargetDate.Value - DateTime.Today).Days : 0;

            return new ProjectSummaryDto(
                p.Id,
                p.Name,
                p.Status,
                p.TotalBudget,
                p.WalletBalance,
                progress,
                p.TargetDate,
                daysRemaining
            );
        }).ToList();

        return ApiResponse<List<ProjectSummaryDto>>.SuccessResponse(dtos);
    }

    public async Task<ApiResponse<ProjectDto>> GetProjectByIdAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.Query()
            .Include(p => p.Client)
            .Include(p => p.Manager)
            .Include(p => p.Tasks)
            .Include(p => p.Budgets)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project == null)
        {
            return ApiResponse<ProjectDto>.FailResponse("Dự án không tồn tại");
        }

        return ApiResponse<ProjectDto>.SuccessResponse(MapToDto(project));
    }

    public async Task<ApiResponse<ProjectDto>> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiResponse<ProjectDto>.FailResponse("Tên dự án là bắt buộc");
        }

        if (request.TotalBudget <= 0)
        {
            return ApiResponse<ProjectDto>.FailResponse("Tổng ngân sách phải lớn hơn 0");
        }

        if (request.TotalBudget > MaxTotalBudget)
        {
            return ApiResponse<ProjectDto>.FailResponse("Tổng ngân sách vượt quá giới hạn cho phép");
        }

        if (request.TargetDate.HasValue && request.TargetDate.Value.Date < request.StartDate.Date)
        {
            return ApiResponse<ProjectDto>.FailResponse("Ngày dự kiến hoàn thành không được sớm hơn ngày bắt đầu");
        }

        // Validate client and manager exist
        var client = await _memberRepository.GetByIdAsync(request.ClientId, cancellationToken);
        var manager = await _memberRepository.GetByIdAsync(request.ManagerId, cancellationToken);

        if (client == null)
        {
            return ApiResponse<ProjectDto>.FailResponse("Khách hàng không tồn tại");
        }

        if (manager == null)
        {
            return ApiResponse<ProjectDto>.FailResponse("Quản lý dự án không tồn tại");
        }

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            ClientId = request.ClientId,
            ManagerId = request.ManagerId,
            StartDate = request.StartDate,
            TargetDate = request.TargetDate,
            TotalBudget = request.TotalBudget,
            Status = ProjectStatus.Planning
        };

        try
        {
            await _projectRepository.AddAsync(project, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ApiResponse<ProjectDto>.FailResponse("Dữ liệu dự án không hợp lệ hoặc vượt giới hạn hệ thống");
        }

        // Reload with navigation properties
        var createdProject = await _projectRepository.Query()
            .Include(p => p.Client)
            .Include(p => p.Manager)
            .FirstOrDefaultAsync(p => p.Id == project.Id, cancellationToken);

        return ApiResponse<ProjectDto>.SuccessResponse(MapToDto(createdProject!), "Tạo dự án thành công");
    }

    public async Task<ApiResponse<ProjectDto>> UpdateProjectAsync(Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.Query()
            .Include(p => p.Client)
            .Include(p => p.Manager)
            .Include(p => p.Tasks)
            .Include(p => p.Budgets)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project == null)
        {
            return ApiResponse<ProjectDto>.FailResponse("Dự án không tồn tại");
        }

        if (request.Name != null)
            project.Name = request.Name;

        if (request.TargetDate.HasValue)
            project.TargetDate = request.TargetDate.Value;

        if (request.TotalBudget.HasValue)
        {
            if (request.TotalBudget.Value <= 0)
            {
                return ApiResponse<ProjectDto>.FailResponse("Tổng ngân sách phải lớn hơn 0");
            }

            if (request.TotalBudget.Value > MaxTotalBudget)
            {
                return ApiResponse<ProjectDto>.FailResponse("Tổng ngân sách vượt quá giới hạn cho phép");
            }

            project.TotalBudget = request.TotalBudget.Value;
        }

        if (project.TargetDate.HasValue && project.TargetDate.Value.Date < project.StartDate.Date)
        {
            return ApiResponse<ProjectDto>.FailResponse("Ngày dự kiến hoàn thành không được sớm hơn ngày bắt đầu");
        }

        if (request.Status.HasValue)
            project.Status = request.Status.Value;

        try
        {
            _projectRepository.Update(project);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ApiResponse<ProjectDto>.FailResponse("Dữ liệu cập nhật không hợp lệ hoặc vượt giới hạn hệ thống");
        }

        return ApiResponse<ProjectDto>.SuccessResponse(MapToDto(project), "Cập nhật dự án thành công");
    }

    public async Task<ApiResponse<bool>> DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken);
        if (project == null)
        {
            return ApiResponse<bool>.FailResponse("Dự án không tồn tại");
        }

        _projectRepository.Remove(project);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "Xóa dự án thành công");
    }

    private static ProjectDto MapToDto(Project project)
    {
        var completedTasks = project.Tasks?.Count(t => t.Status == ProjectTaskStatus.Approved) ?? 0;
        var totalTasks = project.Tasks?.Count ?? 0;
        var progress = totalTasks > 0 ? (double)completedTasks / totalTasks * 100 : 0;
        var spentBudget = project.Budgets?.Sum(b => b.ActualCost) ?? 0;

        return new ProjectDto(
            project.Id,
            project.Name,
            project.ClientId,
            project.Client?.FullName ?? "N/A",
            project.ManagerId,
            project.Manager?.FullName ?? "N/A",
            project.StartDate,
            project.TargetDate,
            project.TotalBudget,
            project.WalletBalance,
            spentBudget,
            project.Status,
            totalTasks,
            completedTasks,
            progress
        );
    }
}
