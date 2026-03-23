using IPM.Domain.Enums;

namespace IPM.Domain.Entities;

public class MediaFile
{
    public Guid Id { get; set; }
    public long? ReferenceId { get; set; }  // TaskId or TransactionId
    public string FileUrl { get; set; } = string.Empty;
    public MediaType Type { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ProjectTask? Task { get; set; }
    public virtual ICollection<AiLog> AiLogs { get; set; } = new List<AiLog>();
}
