using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlayForge.Domain.Entities;

namespace PlayForge.Infrastructure.Persistence.Configurations;

public class VoteConfiguration : IEntityTypeConfiguration<Vote>
{
    public void Configure(EntityTypeBuilder<Vote> builder)
    {
        builder.ToTable("votes");

        // Composite PK: one row per (session, user, game) — up to 5 per user
        builder.HasKey(v => new { v.SessionId, v.UserId, v.GameId });

        builder.Property(v => v.GameId).HasColumnName("game_id");
        builder.Property(v => v.CastAt).HasColumnName("cast_at");
    }
}
