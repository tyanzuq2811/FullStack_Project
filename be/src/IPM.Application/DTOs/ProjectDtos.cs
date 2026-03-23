using IPM.Domain.Enums;

namespace IPM.Application.DTOs.Project;

public record ProjectDto(
    Guid Id,
    string Name,
    Guid ClientId,
    string ClientName,
    Guid ManagerId,
    string ManagerName,
    DateTime StartDate,
    DateTime? TargetDate,
    decimal TotalBudget,
    decimal WalletBalance,
    decimal SpentBudget,
    ProjectStatus Status,
    int TaskCount,
    int CompletedTaskCount,
    double ProgressPercentage
);

public record CreateProjectRequest(
    string Name,
    Guid ClientId,
    Guid ManagerId,
    DateTime StartDate,
    DateTime? TargetDate,
    decimal TotalBudget
);

public record UpdateProjectRequest(
    string? Name,
    DateTime? TargetDate,
    decimal? TotalBudget,
    ProjectStatus? Status
);

public record ProjectSummaryDto(
    Guid Id,
    string Name,
    ProjectStatus Status,
    decimal TotalBudget,
    decimal WalletBalance,
    double ProgressPercentage,
    DateTime? TargetDate,
    int DaysRemaining
);
