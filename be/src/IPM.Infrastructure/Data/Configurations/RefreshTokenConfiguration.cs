using IPM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IPM.Infrastructure.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Token)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.JwtId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.IsUsed)
            .HasDefaultValue(false);

        builder.Property(r => r.IsRevoked)
            .HasDefaultValue(false);

        builder.HasOne(r => r.Member)
            .WithMany(m => m.RefreshTokens)
            .HasForeignKey(r => r.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
