using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlayForge.Domain.Entities;

namespace PlayForge.Infrastructure.Persistence.Configurations;

public class UserGameConfiguration : IEntityTypeConfiguration<UserGame>
{
    public void Configure(EntityTypeBuilder<UserGame> builder)
    {
        builder.ToTable("user_games");
        builder.HasKey(ug => new { ug.UserId, ug.GameId });

        builder.Property(ug => ug.PlaytimeMinutes).HasColumnName("playtime_minutes");
        builder.Property(ug => ug.SyncedAt).HasColumnName("synced_at");

        builder.HasOne(ug => ug.Game)
               .WithMany()
               .HasForeignKey(ug => ug.GameId);
    }
}
