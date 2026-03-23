using IPM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IPM.Infrastructure.Data.Configurations;

public class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("Members");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(m => m.FullName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(m => m.Email)
            .HasMaxLength(255)
            .IsRequired();

        builder.HasIndex(m => m.Email).IsUnique();

        builder.Property(m => m.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(m => m.RankELO)
            .HasDefaultValue(1200);

        builder.Property(m => m.WalletBalance)
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(m => m.IsActive)
            .HasDefaultValue(true);

        builder.Property(m => m.RowVersion)
            .IsRowVersion();
    }
}
