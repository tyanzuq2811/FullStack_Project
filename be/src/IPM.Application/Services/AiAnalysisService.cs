using System.Text.Json;
using Hangfire;
using IPM.Application.DTOs.AI;
using IPM.Application.DTOs.Common;
using IPM.Application.Services.Interfaces;
using IPM.Application.Contracts.SignalR;
using IPM.Domain.Entities;
using IPM.Domain.Enums;
using IPM.Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IPM.Application.Services;

public class AiAnalysisService : IAiAnalysisService
{
    private readonly IAiService _aiService;
    private readonly IRepository<ProjectTask> _taskRepository;
    private readonly IRepository<Project> _projectRepository;
    private readonly IRepository<ProjectBudget> _budgetRepository;
    private readonly IRepository<MediaFile> _mediaRepository;
    private readonly IRepository<AiLog> _aiLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IConfiguration _configuration;

    public AiAnalysisService(
        IAiService aiService,
        IRepository<ProjectTask> taskRepository,
        IRepository<Project> projectRepository,
        IRepository<ProjectBudget> budgetRepository,
        IRepository<MediaFile> mediaRepository,
        IRepository<AiLog> aiLogRepository,
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IEmailService emailService,
        IBackgroundJobClient backgroundJobClient,
        IConfiguration configuration)
    {
        _aiService = aiService;
        _taskRepository = taskRepository;
        _projectRepository = projectRepository;
        _budgetRepository = budgetRepository;
        _mediaRepository = mediaRepository;
        _aiLogRepository = aiLogRepository;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _emailService = emailService;
        _backgroundJobClient = backgroundJobClient;
        _configuration = configuration;
    }

    public async Task<ApiResponse<AnalyzeProgressResponse>> AnalyzeProgressAsync(
        long taskId,
        AnalyzeProgressRequest request,
        Guid requesterMemberId,
        bool isProjectManager,
        bool isSubcontractor,
        CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.Query()
            .Include(t => t.Project)
            .ThenInclude(p => p.Client)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task == null)
        {
            return ApiResponse<AnalyzeProgressResponse>.FailResponse("Công việc không tồn tại");
        }

        var canAccess =
            (isProjectManager && task.Project.ManagerId == requesterMemberId) ||
            (isSubcontractor && task.SubcontractorId == requesterMemberId);

        if (!canAccess)
        {
            return ApiResponse<AnalyzeProgressResponse>.FailResponse("Bạn không có quyền phân tích tiến độ công việc này");
        }

        // Call AI service
        var aiResult = await _aiService.AnalyzeProgressImageAsync(request.ImageBase64, cancellationToken);
        if (aiResult == null)
        {
            return ApiResponse<AnalyzeProgressResponse>.FailResponse("Không thể phân tích hình ảnh");
        }

        // Save media file
        var mediaFile = new MediaFile
        {
            Id = Guid.NewGuid(),
            ReferenceId = taskId,
            FileUrl = $"data:image/jpeg;base64,{request.ImageBase64}",
            Type = MediaType.ProgressPhoto
        };
        await _mediaRepository.AddAsync(mediaFile, cancellationToken);

        // Save AI log
        var aiLog = new AiLog
        {
            MediaId = mediaFile.Id,
            RawResponse = JsonSerializer.Serialize(aiResult),
            Anomalies = aiResult.AnomaliesDetected.Any()
                ? JsonSerializer.Serialize(aiResult.AnomaliesDetected)
                : null,
            Confidence = 0.85 // Default confidence
        };
        await _aiLogRepository.AddAsync(aiLog, cancellationToken);

        // Update task progress
        task.ProgressPct = aiResult.ProgressPct;
        _taskRepository.Update(task);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Notify project group
        await _notificationService.SendAiAnalysisCompletedToGroupAsync($"project_{task.ProjectId}",
            new AiAnalysisCompletedDto(
                mediaFile.Id,
                taskId,
                aiResult.ProgressPct,
                aiResult.AnomaliesDetected,
                aiResult.AnomaliesDetected.Any()));

        await _notificationService.SendTaskStatusChangedToGroupAsync($"project_{task.ProjectId}",
            new TaskStatusChangedDto(
                task.Id,
                task.Name,
                (int)task.Status,
                (int)task.Status,
                aiResult.ProgressPct,
                task.ProjectId));

        // Notify client about progress update
        if (task.Project.ClientId != Guid.Empty)
        {
            await _notificationService.SendNotificationToGroupAsync($"user_{task.Project.ClientId}",
                new NotificationDto(
                    "Cập nhật tiến độ",
                    $"AI đã phân tích tiến độ công việc '{task.Name}': {aiResult.ProgressPct}%",
                    aiResult.AnomaliesDetected.Any() ? "warning" : "info",
                    DateTime.UtcNow));
        }

        return ApiResponse<AnalyzeProgressResponse>.SuccessResponse(new AnalyzeProgressResponse(
            aiResult.ProgressPct,
            aiResult.Status,
            aiResult.AnomaliesDetected,
            aiResult.AnomaliesDetected.Any()
        ), "Phân tích tiến độ thành công");
    }

    public async Task<ApiResponse<AnalyzeInvoiceResponse>> AnalyzeInvoiceAsync(
        AnalyzeInvoiceRequest request,
        Guid requesterMemberId,
        bool isAccountant,
        bool isProjectManager,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.Query()
            .Include(p => p.Manager)
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

        if (project == null)
        {
            return ApiResponse<AnalyzeInvoiceResponse>.FailResponse("Dự án không tồn tại");
        }

        var canAccess = isAccountant || (isProjectManager && project.ManagerId == requesterMemberId);
        if (!canAccess)
        {
            return ApiResponse<AnalyzeInvoiceResponse>.FailResponse("Bạn không có quyền phân tích hóa đơn của dự án này");
        }

        // Call AI service for OCR
        var aiResult = await _aiService.ExtractInvoiceDataAsync(request.ImageBase64, cancellationToken);
        if (aiResult == null)
        {
            return ApiResponse<AnalyzeInvoiceResponse>.FailResponse("Không thể trích xuất dữ liệu hóa đơn");
        }

        // Save media file
        var mediaFile = new MediaFile
        {
            Id = Guid.NewGuid(),
            ReferenceId = request.TaskId,
            FileUrl = $"data:image/jpeg;base64,{request.ImageBase64}",
            Type = MediaType.ReceiptInvoice
        };
        await _mediaRepository.AddAsync(mediaFile, cancellationToken);

        // Save AI log
        var aiLog = new AiLog
        {
            MediaId = mediaFile.Id,
            RawResponse = JsonSerializer.Serialize(aiResult),
            Confidence = 0.90
        };
        await _aiLogRepository.AddAsync(aiLog, cancellationToken);

        // Check against budget
        bool exceedsBudget = false;
        decimal? budgetDifference = null;

        if (request.TaskId.HasValue)
        {
            var task = await _taskRepository.GetByIdAsync(request.TaskId.Value, cancellationToken);
            if (task != null && task.EstimatedCost > 0)
            {
                exceedsBudget = aiResult.TotalAmount > task.EstimatedCost;
                budgetDifference = aiResult.TotalAmount - task.EstimatedCost;

                // If exceeds budget, send alert
                if (exceedsBudget)
                {
                    // Create budget record if not exists
                    var budget = await _budgetRepository.FirstOrDefaultAsync(
                        b => b.TaskId == request.TaskId.Value && b.MaterialName == aiResult.Vendor,
                        cancellationToken);

                    if (budget != null)
                    {
                        budget.ActualCost += aiResult.TotalAmount;
                        _budgetRepository.Update(budget);
                    }
                    else
                    {
                        budget = new ProjectBudget
                        {
                            ProjectId = request.ProjectId,
                            TaskId = request.TaskId.Value,
                            MaterialName = aiResult.Vendor,
                            EstimatedCost = task.EstimatedCost,
                            ActualCost = aiResult.TotalAmount
                        };
                        await _budgetRepository.AddAsync(budget, cancellationToken);
                    }

                    // Enqueue email alert to accountant
                    var accountantEmail = _configuration["Email:AccountingRecipient"];
                    if (string.IsNullOrWhiteSpace(accountantEmail))
                    {
                        accountantEmail = project.Manager.Email;
                    }

                    _backgroundJobClient.Enqueue(() => SendBudgetAlertEmailAsync(
                        accountantEmail,
                        project.Name,
                        task.Name,
                        task.EstimatedCost,
                        aiResult.TotalAmount,
                        aiResult.Vendor));

                    // Real-time notification to project manager
                    await _notificationService.SendNotificationToGroupAsync($"user_{project.ManagerId}",
                        new NotificationDto(
                            "Cảnh báo vượt ngân sách",
                            $"Hóa đơn từ {aiResult.Vendor} ({aiResult.TotalAmount:N0} VNĐ) vượt định mức {task.EstimatedCost:N0} VNĐ",
                            "error",
                            DateTime.UtcNow));
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<AnalyzeInvoiceResponse>.SuccessResponse(new AnalyzeInvoiceResponse(
            aiResult.Vendor,
            aiResult.TotalAmount,
            aiResult.InvoiceDate,
            exceedsBudget,
            budgetDifference
        ), exceedsBudget ? "Cảnh báo: Hóa đơn vượt ngân sách!" : "Trích xuất hóa đơn thành công");
    }

    public async System.Threading.Tasks.Task SendBudgetAlertEmailAsync(
        string toEmail,
        string projectName,
        string taskName,
        decimal estimatedCost,
        decimal actualCost,
        string vendor)
    {
        var subject = $"[IPM] Cảnh báo vượt ngân sách - {projectName}";
        var body = $@"
<html>
<body style='font-family: Arial, sans-serif;'>
    <h2 style='color: #dc2626;'>Cảnh báo Vượt Ngân sách</h2>
    <p>Hệ thống IPM phát hiện hóa đơn vượt định mức ngân sách:</p>
    <table style='border-collapse: collapse; width: 100%; max-width: 500px;'>
        <tr>
            <td style='padding: 8px; border: 1px solid #ddd;'><strong>Dự án:</strong></td>
            <td style='padding: 8px; border: 1px solid #ddd;'>{projectName}</td>
        </tr>
        <tr>
            <td style='padding: 8px; border: 1px solid #ddd;'><strong>Hạng mục:</strong></td>
            <td style='padding: 8px; border: 1px solid #ddd;'>{taskName}</td>
        </tr>
        <tr>
            <td style='padding: 8px; border: 1px solid #ddd;'><strong>Nhà cung cấp:</strong></td>
            <td style='padding: 8px; border: 1px solid #ddd;'>{vendor}</td>
        </tr>
        <tr>
            <td style='padding: 8px; border: 1px solid #ddd;'><strong>Ngân sách dự kiến:</strong></td>
            <td style='padding: 8px; border: 1px solid #ddd;'>{estimatedCost:N0} VNĐ</td>
        </tr>
        <tr style='background-color: #fee2e2;'>
            <td style='padding: 8px; border: 1px solid #ddd;'><strong>Chi phí thực tế:</strong></td>
            <td style='padding: 8px; border: 1px solid #ddd; color: #dc2626;'><strong>{actualCost:N0} VNĐ</strong></td>
        </tr>
        <tr style='background-color: #fee2e2;'>
            <td style='padding: 8px; border: 1px solid #ddd;'><strong>Chênh lệch:</strong></td>
            <td style='padding: 8px; border: 1px solid #ddd; color: #dc2626;'><strong>+{actualCost - estimatedCost:N0} VNĐ</strong></td>
        </tr>
    </table>
    <p style='margin-top: 20px;'>Vui lòng đăng nhập hệ thống để xem chi tiết và xử lý.</p>
    <hr style='margin-top: 30px;'>
    <p style='color: #6b7280; font-size: 12px;'>Email này được gửi tự động từ hệ thống IPM Pro.</p>
</body>
</html>";

        await _emailService.SendEmailAsync(toEmail, subject, body);
    }
}
