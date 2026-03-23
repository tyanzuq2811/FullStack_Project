using IPM.Domain.Enums;

namespace IPM.Domain.Entities;

public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid ClientId { get; set; }
    public Guid ManagerId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? TargetDate { get; set; }
    public decimal TotalBudget { get; set; }
    public decimal WalletBalance { get; set; } = 0;
    public ProjectStatus Status { get; set; } = ProjectStatus.Planning;
    public byte[] RowVersion { get; set; } = null!;

    // Navigation properties
    public virtual Member Client { get; set; } = null!;
    public virtual Member Manager { get; set; } = null!;
    public virtual ICollection<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();
    public virtual ICollection<ProjectBudget> Budgets { get; set; } = new List<ProjectBudget>();
}
