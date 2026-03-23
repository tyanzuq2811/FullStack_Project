namespace IPM.Domain.Entities;

public class ProjectBudget
{
    public long Id { get; set; }
    public Guid ProjectId { get; set; }
    public long? TaskId { get; set; }
    public string? MaterialName { get; set; }
    public decimal EstimatedCost { get; set; }
    public decimal ActualCost { get; set; } = 0;
    public byte[] RowVersion { get; set; } = null!;

    // Navigation properties
    public virtual Project Project { get; set; } = null!;
    public virtual ProjectTask? Task { get; set; }
}
