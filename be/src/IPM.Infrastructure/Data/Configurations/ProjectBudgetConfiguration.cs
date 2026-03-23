using IPM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IPM.Infrastructure.Data.Configurations;

public class ProjectBudgetConfiguration : IEntityTypeConfiguration<ProjectBudget>
{
    public void Configure(EntityTypeBuilder<ProjectBudget> builder)
    {
        builder.ToTable("ProjectBudgets");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.MaterialName)
            .HasMaxLength(200);

        builder.Property(b => b.EstimatedCost)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(b => b.ActualCost)
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(b => b.RowVersion)
            .IsRowVersion();

        builder.HasOne(b => b.Project)
            .WithMany(p => p.Budgets)
            .HasForeignKey(b => b.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Task)
            .WithMany(t => t.Budgets)
            .HasForeignKey(b => b.TaskId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
