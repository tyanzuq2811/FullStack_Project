using IPM.Domain.Entities;
using IPM.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IPM.Infrastructure.Data.Configurations;

public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("WalletTransactions");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(w => w.RefId)
            .HasMaxLength(100);

        builder.Property(w => w.Status)
            .HasDefaultValue(TransactionStatus.Pending);

        builder.Property(w => w.EncryptedSignature)
            .HasMaxLength(256);

        builder.Property(w => w.Description)
            .HasMaxLength(500);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(w => w.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(w => w.Member)
            .WithMany(m => m.WalletTransactions)
            .HasForeignKey(w => w.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
