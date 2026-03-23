using IPM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IPM.Infrastructure.Data.Configurations;

public class MediaFileConfiguration : IEntityTypeConfiguration<MediaFile>
{
    public void Configure(EntityTypeBuilder<MediaFile> builder)
    {
        builder.ToTable("MediaFiles");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.FileUrl)
            .IsRequired();

        builder.HasOne(m => m.Task)
            .WithMany(t => t.MediaFiles)
            .HasForeignKey(m => m.ReferenceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
