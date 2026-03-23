using IPM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IPM.Infrastructure.Data.Configurations;

public class NewsConfiguration : IEntityTypeConfiguration<News>
{
    public void Configure(EntityTypeBuilder<News> builder)
    {
        builder.ToTable("News");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(n => n.Content)
            .IsRequired();

        builder.Property(n => n.IsPinned)
            .HasDefaultValue(false);

        builder.Property(n => n.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()");
    }
}
