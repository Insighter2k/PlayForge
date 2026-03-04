using PlayForge.Domain.Enums;

namespace PlayForge.Domain.Entities;

public class GroupMembership
{
    public Guid           GroupId  { get; private set; }
    public Guid           UserId   { get; private set; }
    public MembershipRole Role     { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }

    public GameGroup? Group { get; private set; }
    public User?      User  { get; private set; }

    // EF constructor
    private GroupMembership() { }

    internal static GroupMembership Create(Guid groupId, Guid userId, MembershipRole role)
    {
        return new GroupMembership
        {
            GroupId  = groupId,
            UserId   = userId,
            Role     = role,
            JoinedAt = DateTimeOffset.UtcNow,
        };
    }
}
