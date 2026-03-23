namespace IPM.Domain.Entities;

public class AiLog
{
    public long Id { get; set; }
    public Guid MediaId { get; set; }
    public string? RawResponse { get; set; }  // JSON response from Gemini
    public string? Anomalies { get; set; }    // JSON Array of detected anomalies
    public double? Confidence { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual MediaFile Media { get; set; } = null!;
}
