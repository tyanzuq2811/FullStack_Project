using IPM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IPM.Infrastructure.Data.Configurations;

public class AiLogConfiguration : IEntityTypeConfiguration<AiLog>
{
    public void Configure(EntityTypeBuilder<AiLog> builder)
    {
        builder.ToTable("AiLogs");
        builder.HasKey(a => a.Id);

        builder.HasOne(a => a.Media)
            .WithMany(m => m.AiLogs)
            .HasForeignKey(a => a.MediaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
