using IPM.Domain.Enums;

namespace IPM.Application.DTOs.Task;

public record TaskDto(
    long Id,
    Guid ProjectId,
    string ProjectName,
    PhaseType PhaseType,
    string Name,
    Guid? SubcontractorId,
    string? SubcontractorName,
    DateTime? StartTime,
    DateTime? EndTime,
    DateTime? TargetDate,
    int ProgressPct,
    ProjectTaskStatus Status,
    decimal EstimatedCost,
    string? ImageUrl,
    string? ApprovedBy,
    DateTime? ApprovedAt
);

public record CreateTaskRequest(
    Guid ProjectId,
    PhaseType PhaseType,
    string Name,
    Guid? SubcontractorId,
    DateTime? StartTime,
    DateTime? TargetDate,
    decimal EstimatedCost
);

public record UpdateTaskRequest(
    string? Name,
    Guid? SubcontractorId,
    DateTime? StartTime,
    DateTime? EndTime,
    DateTime? TargetDate,
    int? ProgressPct,
    ProjectTaskStatus? Status,
    decimal? EstimatedCost
);

public record TaskStatusUpdateRequest(ProjectTaskStatus NewStatus);

public record TaskProgressUpdateRequest(int ProgressPct, string? ImageBase64);

public record TaskApprovalRequest(bool Approved, string? Notes);
