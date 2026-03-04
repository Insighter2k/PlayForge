using PlayForge.Domain.Entities;

namespace PlayForge.Domain.Interfaces;

public interface IVoteSessionRepository
{
    Task<VoteSession?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<VoteSession?> FindActiveByGroupIdAsync(Guid groupId, CancellationToken ct = default);
    Task<IReadOnlyList<VoteSession>> FindClosedByGroupIdAsync(Guid groupId, CancellationToken ct = default);
    Task AddAsync(VoteSession session, CancellationToken ct = default);
    /// <summary>
    /// Toggles the user's vote for a game. Adds if absent (while under maxVotes),
    /// removes if already present. Returns true if added, false if removed.
    /// </summary>
    Task<bool> ToggleVoteAsync(Guid sessionId, Guid userId, Guid gameId, int maxVotes, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetUserVotesAsync(Guid sessionId, Guid userId, CancellationToken ct = default);
    Task<Dictionary<Guid, int>> GetTallyAsync(Guid sessionId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
