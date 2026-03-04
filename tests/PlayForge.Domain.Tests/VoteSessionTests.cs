using FluentAssertions;
using PlayForge.Domain.Entities;
using PlayForge.Domain.Enums;
using PlayForge.Domain.Exceptions;

namespace PlayForge.Domain.Tests;

public class VoteSessionTests
{
    private static Guid[] Candidates(int count) =>
        Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();

    // ── CastVote ─────────────────────────────────────────────────────────────

    [Fact]
    public void CastVote_ReplacesExistingVoteFromSameUser()
    {
        var candidates = Candidates(2);
        var session    = VoteSession.Create(Guid.NewGuid(), candidates);
        var userId     = Guid.NewGuid();

        session.CastVote(userId, candidates[0]);
        session.CastVote(userId, candidates[1]); // replace

        session.Votes.Should().HaveCount(1);
        session.Votes[0].GameId.Should().Be(candidates[1]);
    }

    [Fact]
    public void CastVote_MultipleUsers_AccumulatesVotes()
    {
        var candidates = Candidates(2);
        var session    = VoteSession.Create(Guid.NewGuid(), candidates);

        session.CastVote(Guid.NewGuid(), candidates[0]);
        session.CastVote(Guid.NewGuid(), candidates[1]);

        session.Votes.Should().HaveCount(2);
    }

    [Fact]
    public void CastVote_ThrowsWhenSessionAlreadyClosed()
    {
        var candidates = Candidates(2);
        var session    = VoteSession.Create(Guid.NewGuid(), candidates);
        session.CastVote(Guid.NewGuid(), candidates[0]);
        session.Close();

        var act = () => session.CastVote(Guid.NewGuid(), candidates[0]);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void CastVote_ThrowsForGameNotInCandidates()
    {
        var candidates = Candidates(2);
        var session    = VoteSession.Create(Guid.NewGuid(), candidates);

        var act = () => session.CastVote(Guid.NewGuid(), Guid.NewGuid());

        act.Should().Throw<DomainException>();
    }

    // ── Close ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Close_SetsStatusClosedAndWinnerGameId()
    {
        var candidates = Candidates(2);
        var session    = VoteSession.Create(Guid.NewGuid(), candidates);
        session.CastVote(Guid.NewGuid(), candidates[0]);

        var winnerId = session.Close();

        winnerId.Should().Be(candidates[0]);
        session.Status.Should().Be(VoteSessionStatus.Closed);
        session.WinnerGameId.Should().Be(candidates[0]);
        session.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public void Close_DeterministicTiebreak_PicksLowestGuid()
    {
        var candidates = Candidates(2);
        var session    = VoteSession.Create(Guid.NewGuid(), candidates);

        // 1-1 tie
        session.CastVote(Guid.NewGuid(), candidates[0]);
        session.CastVote(Guid.NewGuid(), candidates[1]);

        var winnerId = session.Close(randomTiebreak: false);

        winnerId.Should().Be(candidates.OrderBy(id => id).First());
    }

    [Fact]
    public void Close_RandomTiebreak_PicksOneOfTheTiedCandidates()
    {
        var candidates = Candidates(2);
        var session    = VoteSession.Create(Guid.NewGuid(), candidates);

        session.CastVote(Guid.NewGuid(), candidates[0]);
        session.CastVote(Guid.NewGuid(), candidates[1]);

        var winnerId = session.Close(randomTiebreak: true);

        candidates.Should().Contain(winnerId);
    }

    [Fact]
    public void Close_NoVotes_ReturnsFirstCandidate()
    {
        var candidates = Candidates(3);
        var session    = VoteSession.Create(Guid.NewGuid(), candidates);

        var winnerId = session.Close(randomTiebreak: false);

        winnerId.Should().Be(candidates[0]);
    }

    [Fact]
    public void Close_AlreadyClosed_ThrowsDomainException()
    {
        var candidates = Candidates(2);
        var session    = VoteSession.Create(Guid.NewGuid(), candidates);
        session.Close();

        var act = () => session.Close();

        act.Should().Throw<DomainException>();
    }

    // ── GetTally ──────────────────────────────────────────────────────────────

    [Fact]
    public void GetTally_ReflectsCurrentVotes()
    {
        var candidates = Candidates(3);
        var session    = VoteSession.Create(Guid.NewGuid(), candidates);

        session.CastVote(Guid.NewGuid(), candidates[0]);
        session.CastVote(Guid.NewGuid(), candidates[0]);
        session.CastVote(Guid.NewGuid(), candidates[1]);

        var tally = session.GetTally();

        tally[candidates[0]].Should().Be(2);
        tally[candidates[1]].Should().Be(1);
        tally.ContainsKey(candidates[2]).Should().BeFalse();
    }
}
