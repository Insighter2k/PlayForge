using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlayForge.Domain.Entities;

namespace PlayForge.Infrastructure.Persistence.Configurations;

public class GroupMembershipConfiguration : IEntityTypeConfiguration<GroupMembership>
{
    public void Configure(EntityTypeBuilder<GroupMembership> builder)
    {
        builder.ToTable("group_memberships");
        builder.HasKey(m => new { m.GroupId, m.UserId });

        builder.Property(m => m.Role).HasColumnName("role");
        builder.Property(m => m.JoinedAt).HasColumnName("joined_at");

        builder.HasOne(m => m.User)
               .WithMany()
               .HasForeignKey(m => m.UserId);
    }
}
