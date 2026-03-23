using IPM.Domain.Enums;

namespace IPM.Domain.Entities;

public class ProjectTask
{
    public long Id { get; set; }
    public Guid ProjectId { get; set; }
    public PhaseType PhaseType { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? SubcontractorId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public DateTime? TargetDate { get; set; }
    public int ProgressPct { get; set; } = 0;
    public ProjectTaskStatus Status { get; set; } = ProjectTaskStatus.ToDo;
    public decimal EstimatedCost { get; set; } = 0;

    // Navigation properties
    public virtual Project Project { get; set; } = null!;
    public virtual Member? Subcontractor { get; set; }
    public virtual ICollection<ProjectBudget> Budgets { get; set; } = new List<ProjectBudget>();
    public virtual ICollection<MediaFile> MediaFiles { get; set; } = new List<MediaFile>();
}
