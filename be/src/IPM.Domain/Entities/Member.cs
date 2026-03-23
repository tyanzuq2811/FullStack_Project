namespace IPM.Domain.Entities;

public class Member
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;  // FK to AspNetUsers
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public double RankELO { get; set; } = 1200;
    public decimal WalletBalance { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public byte[] RowVersion { get; set; } = null!;

    // Navigation properties
    public virtual ICollection<Project> ClientProjects { get; set; } = new List<Project>();
    public virtual ICollection<Project> ManagedProjects { get; set; } = new List<Project>();
    public virtual ICollection<ProjectTask> AssignedTasks { get; set; } = new List<ProjectTask>();
    public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
