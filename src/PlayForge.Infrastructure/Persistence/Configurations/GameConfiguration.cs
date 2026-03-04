using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlayForge.Domain.Entities;

namespace PlayForge.Infrastructure.Persistence.Configurations;

public class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.ToTable("games");
        builder.HasKey(g => g.Id);

        builder.OwnsOne(g => g.AppId, vo =>
        {
            vo.Property(a => a.Value)
              .HasColumnName("app_id")
              .IsRequired();
            vo.HasIndex(a => a.Value).IsUnique();
        });

        builder.Property(g => g.Name).HasColumnName("name").HasMaxLength(256).IsRequired();
        builder.Property(g => g.CoverImageUrl).HasColumnName("cover_image_url").HasMaxLength(512);
        builder.Property(g => g.Platform).HasColumnName("platform");
        builder.Property(g => g.Tags)
               .HasColumnName("tags")
               .HasColumnType("text[]")
               .HasDefaultValueSql("'{}'");
        builder.Property(g => g.Screenshots)
               .HasColumnName("screenshots")
               .HasColumnType("text[]")
               .HasDefaultValueSql("'{}'");
        builder.Property(g => g.ReleaseDate).HasColumnName("release_date").HasMaxLength(32);
        builder.Property(g => g.ReviewSummary).HasColumnName("review_summary").HasMaxLength(64);
        builder.Property(g => g.ReviewScore).HasColumnName("review_score").HasDefaultValue(0);
    }
}
