using Microsoft.EntityFrameworkCore;
using PlayForge.Domain.Entities;
using PlayForge.Domain.Interfaces;
using PlayForge.Domain.ValueObjects;

namespace PlayForge.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db) => _db = db;

    public Task<User?> FindBySteamIdAsync(SteamId steamId, CancellationToken ct = default)
        => _db.Users
              .Include(u => u.Games)
              .FirstOrDefaultAsync(u => u.SteamId.Value == steamId.Value, ct);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Users
              .Include(u => u.Games)
              .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<IReadOnlyList<User>> FindByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var list = await _db.Users
                            .Where(u => ids.Contains(u.Id))
                            .Include(u => u.Games)
                            .ToListAsync(ct);
        return list;
    }

    public async Task<Dictionary<long, User>> FindBySteamIdsAsync(
        IEnumerable<long> steamIds,
        CancellationToken ct = default)
    {
        var ids = steamIds.ToList();
        return await _db.Users
            .Where(u => ids.Contains(u.SteamId.Value))
            .ToDictionaryAsync(u => u.SteamId.Value, ct);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await _db.Users.AddAsync(user, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
