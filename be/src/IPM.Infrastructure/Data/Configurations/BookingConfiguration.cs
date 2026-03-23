using IPM.Domain.Entities;
using IPM.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IPM.Infrastructure.Data.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Price)
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(b => b.Status)
            .HasDefaultValue(BookingStatus.PendingPayment);

        builder.Property(b => b.GroupId)
            .HasMaxLength(50);

        builder.Property(b => b.RowVersion)
            .IsRowVersion();

        builder.HasOne(b => b.Resource)
            .WithMany(r => r.Bookings)
            .HasForeignKey(b => b.ResourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Member)
            .WithMany(m => m.Bookings)
            .HasForeignKey(b => b.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for efficient conflict detection
        builder.HasIndex(b => new { b.ResourceId, b.StartTime, b.EndTime })
            .IsUnique()
            .HasFilter("[Status] <> 2");
    }
}
