using PlayForge.Domain.Entities;

namespace PlayForge.Domain.Interfaces;

public interface IGameRepository
{
    Task<Dictionary<long, Game>> FindByAppIdsAsync(IEnumerable<long> appIds, CancellationToken ct = default);
    Task<Dictionary<Guid, Game>> FindByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
