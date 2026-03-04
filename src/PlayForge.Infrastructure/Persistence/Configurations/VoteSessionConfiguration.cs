using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlayForge.Domain.Entities;

namespace PlayForge.Infrastructure.Persistence.Configurations;

public class VoteSessionConfiguration : IEntityTypeConfiguration<VoteSession>
{
    public void Configure(EntityTypeBuilder<VoteSession> builder)
    {
        builder.ToTable("vote_sessions");
        builder.HasKey(vs => vs.Id);

        builder.Property(vs => vs.GroupId).HasColumnName("group_id");
        builder.Property(vs => vs.Status).HasColumnName("status");
        builder.Property(vs => vs.WinnerGameId).HasColumnName("winner_game_id");
        builder.Property(vs => vs.StartedAt).HasColumnName("started_at");
        builder.Property(vs => vs.ClosedAt).HasColumnName("closed_at");

        // uuid[] native PostgreSQL array column — write-once, read-whole
        builder.Property(vs => vs.CandidateGameIds)
               .HasColumnName("candidate_game_ids")
               .HasColumnType("uuid[]");

        builder.HasMany(vs => vs.Votes)
               .WithOne()
               .HasForeignKey(v => v.SessionId);
    }
}
