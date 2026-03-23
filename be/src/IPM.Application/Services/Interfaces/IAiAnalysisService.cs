using IPM.Application.DTOs.AI;
using IPM.Application.DTOs.Common;

namespace IPM.Application.Services.Interfaces;

public interface IAiAnalysisService
{
    Task<ApiResponse<AnalyzeProgressResponse>> AnalyzeProgressAsync(
        long taskId,
        AnalyzeProgressRequest request,
        Guid requesterMemberId,
        bool isProjectManager,
        bool isSubcontractor,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<AnalyzeInvoiceResponse>> AnalyzeInvoiceAsync(
        AnalyzeInvoiceRequest request,
        Guid requesterMemberId,
        bool isAccountant,
        bool isProjectManager,
        CancellationToken cancellationToken = default);
}
