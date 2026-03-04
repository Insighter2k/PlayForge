using PlayForge.Domain.Entities;

namespace PlayForge.Domain.Interfaces;

public interface IGroupRepository
{
    Task<GameGroup?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<GameGroup?> FindByInviteCodeAsync(string inviteCode, CancellationToken ct = default);
    Task<IReadOnlyList<GameGroup>> FindByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(GameGroup group, CancellationToken ct = default);
    Task DeleteAsync(Guid groupId, CancellationToken ct = default);
    Task<GroupCandidate?> FindCandidateByAppIdAsync(Guid groupId, long appId, CancellationToken ct = default);
    Task AddCandidateAsync(GroupCandidate candidate, CancellationToken ct = default);
    Task RemoveCandidateAsync(Guid groupId, Guid candidateId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
