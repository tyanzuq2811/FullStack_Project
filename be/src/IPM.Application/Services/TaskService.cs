using Hangfire;
using IPM.Application.DTOs.Common;
using IPM.Application.DTOs.Task;
using IPM.Application.Services.Interfaces;
using IPM.Application.Contracts.SignalR;
using IPM.Domain.Entities;
using IPM.Domain.Enums;
using IPM.Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace IPM.Application.Services;

public class TaskService : ITaskService
{
    private readonly IRepository<ProjectTask> _taskRepository;
    private readonly IRepository<Project> _projectRepository;
    private readonly IRepository<Member> _memberRepository;
    private readonly IRepository<WalletTransaction> _transactionRepository;
    private readonly IRepository<AiLog> _aiLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly ICacheService _cacheService;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public TaskService(
        IRepository<ProjectTask> taskRepository,
        IRepository<Project> projectRepository,
        IRepository<Member> memberRepository,
        IRepository<WalletTransaction> transactionRepository,
        IRepository<AiLog> aiLogRepository,
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        ICacheService cacheService,
        IBackgroundJobClient backgroundJobClient)
    {
        _taskRepository = taskRepository;
        _projectRepository = projectRepository;
        _memberRepository = memberRepository;
        _transactionRepository = transactionRepository;
        _aiLogRepository = aiLogRepository;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _cacheService = cacheService;
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task<ApiResponse<List<TaskDto>>> GetTasksByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var tasks = await _taskRepository.Query()
            .Include(t => t.Project)
            .ThenInclude(p => p.Manager)
            .Include(t => t.Subcontractor)
            .Include(t => t.MediaFiles)
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.Status)
            .ThenBy(t => t.TargetDate)
            .ToListAsync(cancellationToken);

        var dtos = tasks.Select(MapToDto).ToList();
        return ApiResponse<List<TaskDto>>.SuccessResponse(dtos);
    }

    public async Task<ApiResponse<List<TaskDto>>> GetMyTasksAsync(Guid subcontractorId, CancellationToken cancellationToken = default)
    {
        var tasks = await _taskRepository.Query()
            .Include(t => t.Project)
            .ThenInclude(p => p.Manager)
            .Include(t => t.Subcontractor)
            .Include(t => t.MediaFiles)
            .Where(t => t.SubcontractorId == subcontractorId)
            .OrderBy(t => t.Status)
            .ThenBy(t => t.TargetDate)
            .ToListAsync(cancellationToken);

        var dtos = tasks.Select(MapToDto).ToList();
        return ApiResponse<List<TaskDto>>.SuccessResponse(dtos);
    }

    public async Task<ApiResponse<TaskDto>> GetTaskByIdAsync(long taskId, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.Query()
            .Include(t => t.Project)
            .ThenInclude(p => p.Manager)
            .Include(t => t.Subcontractor)
            .Include(t => t.MediaFiles)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task == null)
        {
            return ApiResponse<TaskDto>.FailResponse("Công việc không tồn tại");
        }

        return ApiResponse<TaskDto>.SuccessResponse(MapToDto(task));
    }

    public async Task<ApiResponse<TaskDto>> CreateTaskAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);
        if (project == null)
        {
            return ApiResponse<TaskDto>.FailResponse("Dự án không tồn tại");
        }

        if (request.SubcontractorId.HasValue)
        {
            var subcontractor = await _memberRepository.GetByIdAsync(request.SubcontractorId.Value, cancellationToken);
            if (subcontractor == null)
            {
                return ApiResponse<TaskDto>.FailResponse("Nhà thầu phụ không tồn tại");
            }
        }

        var task = new ProjectTask
        {
            ProjectId = request.ProjectId,
            PhaseType = request.PhaseType,
            Name = request.Name,
            SubcontractorId = request.SubcontractorId,
            StartTime = request.StartTime,
            TargetDate = request.TargetDate,
            EstimatedCost = request.EstimatedCost,
            Status = ProjectTaskStatus.ToDo,
            ProgressPct = 0
        };

        await _taskRepository.AddAsync(task, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with navigation properties
        var createdTask = await _taskRepository.Query()
            .Include(t => t.Project)
            .ThenInclude(p => p.Manager)
            .Include(t => t.Subcontractor)
            .Include(t => t.MediaFiles)
            .FirstOrDefaultAsync(t => t.Id == task.Id, cancellationToken);

        // Notify project group
        await _notificationService.SendTaskStatusChangedToGroupAsync($"project_{request.ProjectId}",
            new TaskStatusChangedDto(
                task.Id,
                task.Name,
                -1,
                (int)task.Status,
                task.ProgressPct,
                task.ProjectId));

        return ApiResponse<TaskDto>.SuccessResponse(MapToDto(createdTask!), "Tạo công việc thành công");
    }

    public async Task<ApiResponse<TaskDto>> UpdateTaskAsync(long taskId, UpdateTaskRequest request, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.Query()
            .Include(t => t.Project)
            .ThenInclude(p => p.Manager)
            .Include(t => t.Subcontractor)
            .Include(t => t.MediaFiles)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task == null)
        {
            return ApiResponse<TaskDto>.FailResponse("Công việc không tồn tại");
        }

        if (request.Name != null)
            task.Name = request.Name;

        if (request.SubcontractorId.HasValue)
            task.SubcontractorId = request.SubcontractorId;

        if (request.StartTime.HasValue)
            task.StartTime = request.StartTime;

        if (request.EndTime.HasValue)
            task.EndTime = request.EndTime;

        if (request.TargetDate.HasValue)
            task.TargetDate = request.TargetDate;

        if (request.ProgressPct.HasValue)
            task.ProgressPct = Math.Clamp(request.ProgressPct.Value, 0, 100);

        if (request.Status.HasValue)
            task.Status = request.Status.Value;

        if (request.EstimatedCost.HasValue)
            task.EstimatedCost = request.EstimatedCost.Value;

        _taskRepository.Update(task);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<TaskDto>.SuccessResponse(MapToDto(task), "Cập nhật công việc thành công");
    }

    public async Task<ApiResponse<TaskDto>> UpdateTaskStatusAsync(long taskId, Guid memberId, TaskStatusUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.Query()
            .Include(t => t.Project)
            .ThenInclude(p => p.Manager)
            .Include(t => t.Subcontractor)
            .Include(t => t.MediaFiles)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task == null)
        {
            return ApiResponse<TaskDto>.FailResponse("Công việc không tồn tại");
        }

        // Enforce approval workflow: moving to Approved must go through ApproveTaskAsync
        if (request.NewStatus == ProjectTaskStatus.Approved)
        {
            return ApiResponse<TaskDto>.FailResponse("Không thể chuyển thẳng sang Đã hoàn thành. Vui lòng dùng thao tác nghiệm thu.");
        }

        var isProjectManager = task.Project.ManagerId == memberId;
        var isAssignedSubcontractor = task.SubcontractorId == memberId;

        // Check permission - only project manager or assigned subcontractor can update status
        if (!isProjectManager && !isAssignedSubcontractor)
        {
            return ApiResponse<TaskDto>.FailResponse("Bạn không có quyền cập nhật trạng thái công việc này");
        }

        // PM has full orchestration control over Kanban.
        if (!isProjectManager)
        {
            var isAllowedSubcontractorTransition =
                (task.Status == ProjectTaskStatus.ToDo && request.NewStatus == ProjectTaskStatus.InProgress) ||
                (task.Status == ProjectTaskStatus.InProgress && request.NewStatus == ProjectTaskStatus.Review);

            if (!isAllowedSubcontractorTransition)
            {
                return ApiResponse<TaskDto>.FailResponse("Nhà thầu phụ chỉ được chuyển To Do -> In Progress hoặc In Progress -> Review");
            }
        }

        var oldStatus = task.Status;
        task.Status = request.NewStatus;

        // If moving to InProgress, set start time
        if (request.NewStatus == ProjectTaskStatus.InProgress && !task.StartTime.HasValue)
        {
            task.StartTime = DateTime.UtcNow;
        }

        // If moving to Review, update progress to 100%
        if (request.NewStatus == ProjectTaskStatus.Review)
        {
            task.ProgressPct = 100;
        }

        _taskRepository.Update(task);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Broadcast status change to all clients in project group
        await _notificationService.SendTaskStatusChangedToGroupAsync($"project_{task.ProjectId}",
            new TaskStatusChangedDto(
                task.Id,
                task.Name,
                (int)oldStatus,
                (int)task.Status,
                task.ProgressPct,
                task.ProjectId));

        return ApiResponse<TaskDto>.SuccessResponse(MapToDto(task), "Cập nhật trạng thái thành công");
    }

    public async Task<ApiResponse<TaskDto>> UpdateTaskProgressAsync(long taskId, Guid memberId, TaskProgressUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.Query()
            .Include(t => t.Project)
            .ThenInclude(p => p.Manager)
            .Include(t => t.Subcontractor)
            .Include(t => t.MediaFiles)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task == null)
        {
            return ApiResponse<TaskDto>.FailResponse("Công việc không tồn tại");
        }

        // Check permission - only subcontractor assigned or project manager can update
        if (task.SubcontractorId != memberId && task.Project.ManagerId != memberId)
        {
            return ApiResponse<TaskDto>.FailResponse("Bạn không có quyền cập nhật tiến độ công việc này");
        }

        task.ProgressPct = Math.Clamp(request.ProgressPct, 0, 100);

        // If progress is 100%, suggest moving to Review
        if (task.ProgressPct == 100 && task.Status == ProjectTaskStatus.InProgress)
        {
            task.Status = ProjectTaskStatus.Review;
        }

        _taskRepository.Update(task);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Broadcast progress update
        await _notificationService.SendTaskStatusChangedToGroupAsync($"project_{task.ProjectId}",
            new TaskStatusChangedDto(
                task.Id,
                task.Name,
                (int)task.Status,
                (int)task.Status,
                task.ProgressPct,
                task.ProjectId));

        return ApiResponse<TaskDto>.SuccessResponse(MapToDto(task), "Cập nhật tiến độ thành công");
    }

    public async Task<ApiResponse<TaskDto>> ApproveTaskAsync(long taskId, Guid managerId, TaskApprovalRequest request, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.Query()
            .Include(t => t.Project)
            .ThenInclude(p => p.Manager)
            .Include(t => t.Subcontractor)
            .Include(t => t.MediaFiles)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task == null)
        {
            return ApiResponse<TaskDto>.FailResponse("Công việc không tồn tại");
        }

        // Check if manager is authorized
        if (task.Project.ManagerId != managerId)
        {
            return ApiResponse<TaskDto>.FailResponse("Bạn không có quyền nghiệm thu công việc này");
        }

        // Only tasks in Review status can be approved
        if (task.Status != ProjectTaskStatus.Review)
        {
            return ApiResponse<TaskDto>.FailResponse("Công việc phải ở trạng thái chờ nghiệm thu");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var oldStatus = task.Status;

            if (request.Approved)
            {
                task.Status = ProjectTaskStatus.Approved;
                task.EndTime = DateTime.UtcNow;

                // Process payment to subcontractor if assigned
                if (task.SubcontractorId.HasValue && task.EstimatedCost > 0)
                {
                    // Check project budget
                    var project = await _projectRepository.Query()
                        .Include(p => p.Tasks.Where(t => t.Status == ProjectTaskStatus.Approved))
                        .FirstOrDefaultAsync(p => p.Id == task.ProjectId, cancellationToken);

                    if (project == null)
                    {
                        await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                        return ApiResponse<TaskDto>.FailResponse("Dự án không tồn tại");
                    }

                    var spentBudget = project.Tasks.Sum(t => t.EstimatedCost);
                    var remainingBudget = project.TotalBudget - spentBudget;

                    // Hard-stop against both budget ceiling and actual project wallet liquidity.
                    if (project.WalletBalance < task.EstimatedCost)
                    {
                        await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                        return ApiResponse<TaskDto>.FailResponse(
                            $"Ví dự án không đủ số dư. Còn lại: {project.WalletBalance:N0} VNĐ, cần: {task.EstimatedCost:N0} VNĐ");
                    }

                    // Hard-stop: Check if budget is sufficient
                    if (remainingBudget < task.EstimatedCost)
                    {
                        await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                        return ApiResponse<TaskDto>.FailResponse(
                            $"Ngân sách dự án không đủ. Còn lại: {remainingBudget:N0} VNĐ, cần: {task.EstimatedCost:N0} VNĐ");
                    }

                    // Transfer to subcontractor wallet
                    var subcontractor = await _memberRepository.GetByIdAsync(task.SubcontractorId.Value, cancellationToken);
                    if (subcontractor != null)
                    {
                        project.WalletBalance -= task.EstimatedCost;
                        _projectRepository.Update(project);

                        subcontractor.WalletBalance += task.EstimatedCost;
                        _memberRepository.Update(subcontractor);

                        var projectDebitTransaction = new WalletTransaction
                        {
                            Id = Guid.NewGuid(),
                            MemberId = project.ClientId,
                            ProjectId = project.Id,
                            Category = TransactionCategory.SubcontractorPayment,
                            Amount = task.EstimatedCost,
                            TransType = TransactionType.Debit,
                            Status = TransactionStatus.Success,
                            Description = $"Giải ngân từ ví dự án cho nghiệm thu: {task.Name}",
                            RefId = task.Id.ToString()
                        };
                        await _transactionRepository.AddAsync(projectDebitTransaction, cancellationToken);

                        // Create wallet transaction
                        var transaction = new WalletTransaction
                        {
                            Id = Guid.NewGuid(),
                            MemberId = task.SubcontractorId.Value,
                            ProjectId = project.Id,
                            Category = TransactionCategory.SubcontractorPayment,
                            Amount = task.EstimatedCost,
                            TransType = TransactionType.Credit,
                            Status = TransactionStatus.Success,
                            Description = $"Thanh toán nghiệm thu: {task.Name}",
                            RefId = task.Id.ToString()
                        };
                        await _transactionRepository.AddAsync(transaction, cancellationToken);

                        // Notify subcontractor
                        await _notificationService.SendWalletBalanceChangedToGroupAsync($"user_{task.SubcontractorId}",
                            new WalletBalanceChangedDto(
                                subcontractor.Id,
                                subcontractor.WalletBalance,
                                task.EstimatedCost,
                                "Credit"));

                        await _notificationService.SendNotificationToGroupAsync($"user_{task.SubcontractorId}",
                            new NotificationDto(
                                "Thanh toán nghiệm thu",
                                $"Bạn đã nhận được {task.EstimatedCost:N0} VNĐ từ nghiệm thu công việc: {task.Name}",
                                "success",
                                DateTime.UtcNow));

                        // Update ELO for subcontractor
                        bool onTime = !task.TargetDate.HasValue || task.EndTime <= task.TargetDate;

                        // Get anomaly count from AI logs
                        var anomalyCount = await _aiLogRepository.Query()
                            .Include(log => log.Media)
                            .Where(log => log.Media.ReferenceId == taskId && !string.IsNullOrEmpty(log.Anomalies))
                            .CountAsync(cancellationToken);

                        // Calculate and update ELO directly
                        double eloChange = onTime ? 25 : -15; // Bonus/penalty for on-time/late
                        eloChange -= anomalyCount * 5; // Penalty for anomalies
                        subcontractor.RankELO = Math.Max(0, Math.Min(3000, subcontractor.RankELO + eloChange));
                        _memberRepository.Update(subcontractor);

                        // Update Redis leaderboard
                        await _cacheService.AddToSortedSetAsync("leaderboard:elo", subcontractor.Id.ToString(), subcontractor.RankELO);
                    }
                }
            }
            else
            {
                task.Status = ProjectTaskStatus.Blocked;
            }

            _taskRepository.Update(task);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            // Broadcast status change
            await _notificationService.SendTaskStatusChangedToGroupAsync($"project_{task.ProjectId}",
                new TaskStatusChangedDto(
                    task.Id,
                    task.Name,
                    (int)oldStatus,
                    (int)task.Status,
                    task.ProgressPct,
                    task.ProjectId));

            return ApiResponse<TaskDto>.SuccessResponse(MapToDto(task),
                request.Approved ? "Nghiệm thu và giải ngân thành công" : "Đã từ chối nghiệm thu");
        }
        catch (DbUpdateConcurrencyException)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return ApiResponse<TaskDto>.FailResponse("Có xung đột dữ liệu, vui lòng thử lại");
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ApiResponse<bool>> DeleteTaskAsync(long taskId, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.GetByIdAsync(taskId, cancellationToken);
        if (task == null)
        {
            return ApiResponse<bool>.FailResponse("Công việc không tồn tại");
        }

        _taskRepository.Remove(task);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "Xóa công việc thành công");
    }

    private TaskDto MapToDto(ProjectTask task)
    {
        var latestProgressMedia = task.MediaFiles
            .Where(m => m.Type == MediaType.ProgressPhoto)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefault();

        var approvedAt = task.Status == ProjectTaskStatus.Approved
            ? task.EndTime
            : latestProgressMedia?.CreatedAt;

        var approvedBy = task.Status == ProjectTaskStatus.Approved
            ? task.Project?.Manager?.FullName
            : latestProgressMedia != null ? "AI Analyzer" : null;

        return new TaskDto(
            task.Id,
            task.ProjectId,
            task.Project?.Name ?? "N/A",
            task.PhaseType,
            task.Name,
            task.SubcontractorId,
            task.Subcontractor?.FullName,
            task.StartTime,
            task.EndTime,
            task.TargetDate,
            task.ProgressPct,
            task.Status,
            task.EstimatedCost,
            latestProgressMedia?.FileUrl,
            approvedBy,
            approvedAt
        );
    }
}
