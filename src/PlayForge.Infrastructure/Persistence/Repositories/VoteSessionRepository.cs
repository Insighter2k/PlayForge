using Microsoft.EntityFrameworkCore;
using PlayForge.Domain.Entities;
using PlayForge.Domain.Enums;
using PlayForge.Domain.Interfaces;

namespace PlayForge.Infrastructure.Persistence.Repositories;

public class VoteSessionRepository : IVoteSessionRepository
{
    private readonly AppDbContext _db;

    public VoteSessionRepository(AppDbContext db) => _db = db;

    public Task<VoteSession?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.VoteSessions
              .Include(vs => vs.Votes)
              .FirstOrDefaultAsync(vs => vs.Id == id, ct);

    public Task<VoteSession?> FindActiveByGroupIdAsync(Guid groupId, CancellationToken ct = default)
        => _db.VoteSessions
              .Include(vs => vs.Votes)
              .FirstOrDefaultAsync(vs => vs.GroupId == groupId && vs.Status == VoteSessionStatus.Active, ct);

    public async Task<IReadOnlyList<VoteSession>> FindClosedByGroupIdAsync(Guid groupId, CancellationToken ct = default)
    {
        var list = await _db.VoteSessions
                            .Include(vs => vs.Votes)
                            .Where(vs => vs.GroupId == groupId && vs.Status == VoteSessionStatus.Closed)
                            .OrderByDescending(vs => vs.ClosedAt)
                            .ToListAsync(ct);
        return list;
    }

    public async Task AddAsync(VoteSession session, CancellationToken ct = default)
        => await _db.VoteSessions.AddAsync(session, ct);

    /// <summary>
    /// Toggles a vote. With (SessionId, UserId, GameId) as the composite PK, each
    /// game-vote is a distinct row — no tracker conflicts, plain tracked add/remove.
    /// Returns true if the vote was added, false if it was removed.
    /// </summary>
    public async Task<bool> ToggleVoteAsync(
        Guid sessionId, Guid userId, Guid gameId, int maxVotes, CancellationToken ct = default)
    {
        var existing = await _db.Votes
            .FirstOrDefaultAsync(v => v.SessionId == sessionId
                                   && v.UserId    == userId
                                   && v.GameId    == gameId, ct);

        if (existing is not null)
        {
            _db.Votes.Remove(existing);
            return false;
        }

        var currentCount = await _db.Votes
            .CountAsync(v => v.SessionId == sessionId && v.UserId == userId, ct);

        if (currentCount >= maxVotes)
            throw new Domain.Exceptions.DomainException(
                $"You can vote for at most {maxVotes} games.");

        await _db.Votes.AddAsync(Vote.Create(sessionId, userId, gameId), ct);
        return true;
    }

    public async Task<IReadOnlyList<Guid>> GetUserVotesAsync(
        Guid sessionId, Guid userId, CancellationToken ct = default)
        => await _db.Votes
                    .AsNoTracking()
                    .Where(v => v.SessionId == sessionId && v.UserId == userId)
                    .Select(v => v.GameId)
                    .ToListAsync(ct);

    public Task<Dictionary<Guid, int>> GetTallyAsync(Guid sessionId, CancellationToken ct = default)
        => _db.Votes
              .AsNoTracking()
              .Where(v => v.SessionId == sessionId)
              .GroupBy(v => v.GameId)
              .Select(g => new { GameId = g.Key, Count = g.Count() })
              .ToDictionaryAsync(x => x.GameId, x => x.Count, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
