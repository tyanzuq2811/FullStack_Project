using IPM.Domain.Enums;

namespace IPM.Domain.Entities;

public class Booking
{
    public Guid Id { get; set; }
    public int ResourceId { get; set; }
    public Guid MemberId { get; set; }
    public Guid ProjectId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public decimal Price { get; set; } = 0;
    public BookingStatus Status { get; set; } = BookingStatus.PendingPayment;
    public string? GroupId { get; set; }  // For recurring bookings
    public byte[] RowVersion { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Resource Resource { get; set; } = null!;
    public virtual Member Member { get; set; } = null!;
    public virtual Project Project { get; set; } = null!;
}
