using IPM.Domain.Enums;

namespace IPM.Domain.Entities;

public class WalletTransaction
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public Guid? ProjectId { get; set; }
    public TransactionCategory Category { get; set; }
    public decimal Amount { get; set; }
    public TransactionType TransType { get; set; }
    public string? RefId { get; set; }  // Reference to Task, Project, or Booking
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public string? EncryptedSignature { get; set; }  // SHA256 Hash for anti-fraud
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Member Member { get; set; } = null!;
}
