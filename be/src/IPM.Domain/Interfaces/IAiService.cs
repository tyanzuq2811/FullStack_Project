namespace IPM.Domain.Interfaces;

public class AiProgressResult
{
    public int ProgressPct { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<string> AnomaliesDetected { get; set; } = new();
}

public class AiInvoiceResult
{
    public string Vendor { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime? InvoiceDate { get; set; }
}

public interface IAiService
{
    Task<AiProgressResult?> AnalyzeProgressImageAsync(string base64Image, CancellationToken cancellationToken = default);
    Task<AiInvoiceResult?> ExtractInvoiceDataAsync(string base64Image, CancellationToken cancellationToken = default);
}
