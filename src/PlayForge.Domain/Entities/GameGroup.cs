using PlayForge.Domain.Enums;
using PlayForge.Domain.Exceptions;

namespace PlayForge.Domain.Entities;

public class GameGroup
{
    public Guid   Id         { get; private set; }
    public string Name       { get; private set; } = string.Empty;
    public string InviteCode { get; private set; } = string.Empty;
    public Guid   HostUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<GroupMembership> _memberships = [];
    private readonly List<VoteSession>     _voteSessions = [];
    private readonly List<GroupCandidate>  _candidates = [];

    public IReadOnlyList<GroupMembership> Memberships  => _memberships.AsReadOnly();
    public IReadOnlyList<VoteSession>     VoteSessions => _voteSessions.AsReadOnly();
    public IReadOnlyList<GroupCandidate>  Candidates   => _candidates.AsReadOnly();

    // EF constructor
    private GameGroup() { }

    public static GameGroup Create(Guid hostUserId, string name)
    {
        var group = new GameGroup
        {
            Id         = Guid.NewGuid(),
            Name       = name,
            HostUserId = hostUserId,
            InviteCode = GenerateInviteCode(),
            CreatedAt  = DateTimeOffset.UtcNow,
        };
        group._memberships.Add(GroupMembership.Create(group.Id, hostUserId, MembershipRole.Host));
        return group;
    }

    public GroupMembership AddMember(Guid userId, MembershipRole role = MembershipRole.Member)
    {
        var existing = _memberships.FirstOrDefault(m => m.UserId == userId);
        if (existing is not null) return existing;

        var membership = GroupMembership.Create(Id, userId, role);
        _memberships.Add(membership);
        return membership;
    }

    public VoteSession StartVoteSession(IEnumerable<Guid> candidateGameIds)
    {
        if (_voteSessions.Any(vs => vs.Status == VoteSessionStatus.Active))
            throw new VoteSessionAlreadyActiveException(Id);

        var session = VoteSession.Create(Id, candidateGameIds);
        _voteSessions.Add(session);
        return session;
    }

    public GroupCandidate AddCandidate(Guid addedByUserId, long appId, string name, string coverImageUrl)
    {
        var existing = _candidates.FirstOrDefault(c => c.AppId == appId);
        if (existing is not null) return existing;

        var candidate = GroupCandidate.Create(Id, addedByUserId, appId, name, coverImageUrl);
        _candidates.Add(candidate);
        return candidate;
    }

    public void RemoveCandidate(Guid candidateId)
    {
        var candidate = _candidates.FirstOrDefault(c => c.Id == candidateId);
        if (candidate is not null) _candidates.Remove(candidate);
    }

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, 8)
            .Select(_ => chars[Random.Shared.Next(chars.Length)])
            .ToArray());
    }
}
