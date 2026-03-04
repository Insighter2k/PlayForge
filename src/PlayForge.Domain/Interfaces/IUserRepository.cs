using PlayForge.Domain.Entities;
using PlayForge.Domain.ValueObjects;

namespace PlayForge.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> FindBySteamIdAsync(SteamId steamId, CancellationToken ct = default);
    Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<User>> FindByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<Dictionary<long, User>> FindBySteamIdsAsync(IEnumerable<long> steamIds, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
