using FluentAssertions;
using PlayForge.Domain.Entities;
using PlayForge.Domain.Enums;
using PlayForge.Domain.Exceptions;

namespace PlayForge.Domain.Tests;

public class GameGroupTests
{
    private static Guid[] Candidates(int count) =>
        Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();

    [Fact]
    public void Create_AddsHostAsMemberWithHostRole()
    {
        var hostId = Guid.NewGuid();

        var group = GameGroup.Create(hostId, "Test Group");

        group.Memberships.Should().HaveCount(1);
        group.Memberships[0].UserId.Should().Be(hostId);
        group.Memberships[0].Role.Should().Be(MembershipRole.Host);
    }

    [Fact]
    public void StartVoteSession_ThrowsIfActiveSessionExists()
    {
        var group      = GameGroup.Create(Guid.NewGuid(), "Test Group");
        var candidates = Candidates(2);
        group.StartVoteSession(candidates);

        var act = () => group.StartVoteSession(candidates);

        act.Should().Throw<VoteSessionAlreadyActiveException>();
    }

    [Fact]
    public void StartVoteSession_Succeeds_WhenPreviousSessionWasClosed()
    {
        var group      = GameGroup.Create(Guid.NewGuid(), "Test Group");
        var candidates = Candidates(2);

        var first = group.StartVoteSession(candidates);
        first.Close();

        var second = group.StartVoteSession(candidates);

        second.Should().NotBeNull();
        second.Status.Should().Be(VoteSessionStatus.Active);
    }

    [Fact]
    public void StartVoteSession_CandidateGameIds_MatchInput()
    {
        var group      = GameGroup.Create(Guid.NewGuid(), "Test Group");
        var candidates = Candidates(3);

        var session = group.StartVoteSession(candidates);

        session.CandidateGameIds.Should().BeEquivalentTo(candidates);
    }

    [Fact]
    public void AddMember_IsIdempotent()
    {
        var hostId = Guid.NewGuid();
        var group  = GameGroup.Create(hostId, "Test Group");
        var userId = Guid.NewGuid();

        var first  = group.AddMember(userId);
        var second = group.AddMember(userId); // duplicate

        group.Memberships.Where(m => m.UserId == userId).Should().HaveCount(1);
        second.Should().BeSameAs(first);
    }

    [Fact]
    public void AddMember_Host_ReturnsExistingMembership()
    {
        var hostId = Guid.NewGuid();
        var group  = GameGroup.Create(hostId, "Test Group");

        var result = group.AddMember(hostId);

        group.Memberships.Should().HaveCount(1);
        result.Role.Should().Be(MembershipRole.Host);
    }
}
