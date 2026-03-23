namespace IPM.Application.DTOs.AI;

public record AnalyzeProgressRequest(
    long TaskId,
    string ImageBase64
);

public record AnalyzeProgressResponse(
    int ProgressPct,
    string Status,
    List<string> AnomaliesDetected,
    bool HasAnomalies
);

public record AnalyzeInvoiceRequest(
    Guid ProjectId,
    long? TaskId,
    string ImageBase64
);

public record AnalyzeInvoiceResponse(
    string Vendor,
    decimal TotalAmount,
    DateTime? InvoiceDate,
    bool ExceedsBudget,
    decimal? BudgetDifference
);
