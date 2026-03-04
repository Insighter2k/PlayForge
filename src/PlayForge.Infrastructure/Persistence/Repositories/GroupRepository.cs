using Microsoft.EntityFrameworkCore;
using PlayForge.Domain.Entities;
using PlayForge.Domain.Interfaces;

namespace PlayForge.Infrastructure.Persistence.Repositories;

public class GroupRepository : IGroupRepository
{
    private readonly AppDbContext _db;

    public GroupRepository(AppDbContext db) => _db = db;

    public Task<GameGroup?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.GameGroups
              .Include(g => g.Memberships)
              .Include(g => g.VoteSessions)
              .Include(g => g.Candidates)
              .FirstOrDefaultAsync(g => g.Id == id, ct);

    public Task<GameGroup?> FindByInviteCodeAsync(string inviteCode, CancellationToken ct = default)
        => _db.GameGroups
              .Include(g => g.Memberships)
              .FirstOrDefaultAsync(g => g.InviteCode == inviteCode, ct);

    public async Task<IReadOnlyList<GameGroup>> FindByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var list = await _db.GameGroups
                            .Include(g => g.Memberships)
                            .Include(g => g.VoteSessions)
                            .Where(g => g.Memberships.Any(m => m.UserId == userId))
                            .ToListAsync(ct);
        return list;
    }

    public async Task AddAsync(GameGroup group, CancellationToken ct = default)
        => await _db.GameGroups.AddAsync(group, ct);

    public Task DeleteAsync(Guid groupId, CancellationToken ct = default)
        => _db.GameGroups.Where(g => g.Id == groupId).ExecuteDeleteAsync(ct);

    public Task<GroupCandidate?> FindCandidateByAppIdAsync(Guid groupId, long appId, CancellationToken ct = default)
        => _db.GroupCandidates.FirstOrDefaultAsync(c => c.GroupId == groupId && c.AppId == appId, ct);

    public async Task AddCandidateAsync(GroupCandidate candidate, CancellationToken ct = default)
        => await _db.GroupCandidates.AddAsync(candidate, ct);

    public async Task RemoveCandidateAsync(Guid groupId, Guid candidateId, CancellationToken ct = default)
    {
        var candidate = await _db.GroupCandidates
            .FirstOrDefaultAsync(c => c.Id == candidateId && c.GroupId == groupId, ct);
        if (candidate is not null)
            _db.GroupCandidates.Remove(candidate);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
