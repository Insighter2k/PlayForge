using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlayForge.Domain.Entities;
using PlayForge.Domain.ValueObjects;

namespace PlayForge.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        builder.OwnsOne(u => u.SteamId, vo =>
        {
            vo.Property(s => s.Value)
              .HasColumnName("steam_id")
              .IsRequired();
            vo.HasIndex(s => s.Value).IsUnique();
        });

        builder.Property(u => u.DisplayName).HasColumnName("display_name").HasMaxLength(128).IsRequired();
        builder.Property(u => u.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(512);
        builder.Property(u => u.LastSyncedAt).HasColumnName("last_synced_at");

        builder.HasMany(u => u.Games)
               .WithOne(ug => ug.User)
               .HasForeignKey(ug => ug.UserId);
    }
}
