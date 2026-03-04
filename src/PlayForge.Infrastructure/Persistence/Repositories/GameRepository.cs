using Microsoft.EntityFrameworkCore;
using PlayForge.Domain.Entities;
using PlayForge.Domain.Interfaces;

namespace PlayForge.Infrastructure.Persistence.Repositories;

public class GameRepository : IGameRepository
{
    private readonly AppDbContext _db;

    public GameRepository(AppDbContext db) => _db = db;

    public async Task<Dictionary<long, Game>> FindByAppIdsAsync(
        IEnumerable<long> appIds,
        CancellationToken ct = default)
    {
        var ids = appIds.ToList();
        return await _db.Games
            .Where(g => ids.Contains(g.AppId.Value))
            .ToDictionaryAsync(g => g.AppId.Value, ct);
    }

    public async Task<Dictionary<Guid, Game>> FindByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken ct = default)
    {
        var idList = ids.ToList();
        return await _db.Games
            .Where(g => idList.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, ct);
    }

    public async Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default)
        => await _db.Games.AddRangeAsync(games, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
