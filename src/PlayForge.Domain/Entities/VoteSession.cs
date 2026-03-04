using PlayForge.Domain.Enums;
using PlayForge.Domain.Exceptions;

namespace PlayForge.Domain.Entities;

public class VoteSession
{
    public Guid              Id               { get; private set; }
    public Guid              GroupId          { get; private set; }
    public VoteSessionStatus Status           { get; private set; }
    public Guid[]            CandidateGameIds { get; private set; } = [];
    public Guid?             WinnerGameId     { get; private set; }
    public DateTimeOffset    StartedAt        { get; private set; }
    public DateTimeOffset?   ClosedAt         { get; private set; }

    private readonly List<Vote> _votes = [];
    public IReadOnlyList<Vote> Votes => _votes.AsReadOnly();

    // EF constructor
    private VoteSession() { }

    public static VoteSession Create(Guid groupId, IEnumerable<Guid> candidateGameIds)
    {
        return new VoteSession
        {
            Id               = Guid.NewGuid(),
            GroupId          = groupId,
            Status           = VoteSessionStatus.Active,
            CandidateGameIds = candidateGameIds.ToArray(),
            StartedAt        = DateTimeOffset.UtcNow,
        };
    }

    public void CastVote(Guid userId, Guid gameId)
    {
        if (Status != VoteSessionStatus.Active)
            throw new DomainException("Cannot vote on a closed session.");

        if (!CandidateGameIds.Contains(gameId))
            throw new DomainException($"Game {gameId} is not a candidate in this session.");

        var existing = _votes.FirstOrDefault(v => v.UserId == userId);
        if (existing is not null)
            _votes.Remove(existing);

        _votes.Add(Vote.Create(Id, userId, gameId));
    }

    public Guid Close(bool randomTiebreak = false)
    {
        if (Status != VoteSessionStatus.Active)
            throw new DomainException("Session is already closed.");

        WinnerGameId = DetermineWinner(randomTiebreak);
        Status       = VoteSessionStatus.Closed;
        ClosedAt     = DateTimeOffset.UtcNow;
        return WinnerGameId.Value;
    }

    public IReadOnlyDictionary<Guid, int> GetTally()
    {
        return _votes
            .GroupBy(v => v.GameId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private Guid DetermineWinner(bool randomTiebreak)
    {
        if (_votes.Count == 0)
        {
            // No votes — pick first candidate (or random if tiebreak enabled)
            return randomTiebreak
                ? CandidateGameIds[Random.Shared.Next(CandidateGameIds.Length)]
                : CandidateGameIds[0];
        }

        var tally   = GetTally();
        var maxVotes = tally.Values.Max();
        var leaders  = tally.Where(kv => kv.Value == maxVotes).Select(kv => kv.Key).ToList();

        if (leaders.Count == 1) return leaders[0];

        return randomTiebreak
            ? leaders[Random.Shared.Next(leaders.Count)]
            : leaders.OrderBy(id => id).First(); // deterministic fallback
    }
}
