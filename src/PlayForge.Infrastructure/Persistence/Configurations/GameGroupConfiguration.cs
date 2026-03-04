using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlayForge.Domain.Entities;

namespace PlayForge.Infrastructure.Persistence.Configurations;

public class GameGroupConfiguration : IEntityTypeConfiguration<GameGroup>
{
    public void Configure(EntityTypeBuilder<GameGroup> builder)
    {
        builder.ToTable("game_groups");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
        builder.Property(g => g.InviteCode).HasColumnName("invite_code").HasMaxLength(8).IsRequired();
        builder.Property(g => g.HostUserId).HasColumnName("host_user_id");
        builder.Property(g => g.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(g => g.InviteCode).IsUnique();

        builder.HasMany(g => g.Memberships)
               .WithOne(m => m.Group)
               .HasForeignKey(m => m.GroupId);

        builder.HasMany(g => g.VoteSessions)
               .WithOne()
               .HasForeignKey(vs => vs.GroupId);
    }
}
