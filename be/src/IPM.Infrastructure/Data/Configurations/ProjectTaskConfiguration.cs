using IPM.Domain.Entities;
using IPM.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IPM.Infrastructure.Data.Configurations;

public class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    public void Configure(EntityTypeBuilder<ProjectTask> builder)
    {
        builder.ToTable("Tasks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.ProgressPct)
            .HasDefaultValue(0);

        builder.Property(t => t.Status)
            .HasDefaultValue(ProjectTaskStatus.ToDo);

        builder.Property(t => t.EstimatedCost)
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.HasOne(t => t.Project)
            .WithMany(p => p.Tasks)
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Subcontractor)
            .WithMany(m => m.AssignedTasks)
            .HasForeignKey(t => t.SubcontractorId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
