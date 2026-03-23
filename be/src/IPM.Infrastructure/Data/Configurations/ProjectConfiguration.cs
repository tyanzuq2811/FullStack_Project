using IPM.Domain.Entities;
using IPM.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IPM.Infrastructure.Data.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.TotalBudget)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.WalletBalance)
            .HasPrecision(18, 2)
            .HasDefaultValue(0m);

        builder.Property(p => p.Status)
            .HasDefaultValue(ProjectStatus.Planning);

        builder.Property(p => p.RowVersion)
            .IsRowVersion();

        builder.HasOne(p => p.Client)
            .WithMany(m => m.ClientProjects)
            .HasForeignKey(p => p.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Manager)
            .WithMany(m => m.ManagedProjects)
            .HasForeignKey(p => p.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
