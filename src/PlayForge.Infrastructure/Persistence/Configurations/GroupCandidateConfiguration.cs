using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlayForge.Domain.Entities;

namespace PlayForge.Infrastructure.Persistence.Configurations;

public class GroupCandidateConfiguration : IEntityTypeConfiguration<GroupCandidate>
{
    public void Configure(EntityTypeBuilder<GroupCandidate> builder)
    {
        builder.ToTable("group_candidates");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.GroupId).HasColumnName("group_id");
        builder.Property(c => c.AddedByUserId).HasColumnName("added_by_user_id");
        builder.Property(c => c.AppId).HasColumnName("app_id");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(256).IsRequired();
        builder.Property(c => c.CoverImageUrl).HasColumnName("cover_image_url").HasMaxLength(512);
        builder.Property(c => c.AddedAt).HasColumnName("added_at");

        builder.HasOne(c => c.Group)
               .WithMany(g => g.Candidates)
               .HasForeignKey(c => c.GroupId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
