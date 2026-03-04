using PlayForge.Application.DTOs;

namespace PlayForge.Application.Interfaces;

public interface IVoteService
{
    Task<VoteSessionDto>  StartSessionAsync(Guid groupId, Guid hostUserId, CancellationToken ct = default);
    Task<VoteSessionDto?> GetActiveSessionAsync(Guid groupId, CancellationToken ct = default);
    Task<IReadOnlyList<GameCandidateDto>> GetSessionCandidatesAsync(Guid sessionId, CancellationToken ct = default);
    Task<VoteTallyDto>    GetSessionTallyAsync(Guid sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetUserVotesAsync(Guid sessionId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<VoteHistoryEntryDto>> GetGroupHistoryAsync(Guid groupId, CancellationToken ct = default);
    /// <summary>Toggles a vote. Returns the updated tally.</summary>
    Task<VoteTallyDto>    CastVoteAsync(Guid sessionId, Guid userId, Guid gameId, CancellationToken ct = default);
    Task<VoteSessionDto>  CloseSessionAsync(Guid sessionId, Guid groupId, Guid hostUserId, CancellationToken ct = default);
}
