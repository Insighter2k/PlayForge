using PlayForge.Application.DTOs;
using PlayForge.Application.Interfaces;
using PlayForge.Domain.Entities;
using PlayForge.Domain.Enums;
using PlayForge.Domain.Interfaces;
using PlayForge.Domain.ValueObjects;

namespace PlayForge.Application.Services;

public class LibraryService : ILibraryService
{
    private const int StalenessHours = 1;

    private readonly IUserRepository            _users;
    private readonly IGameRepository            _games;
    private readonly IPlatformConnectorRegistry _registry;

    public LibraryService(
        IUserRepository            users,
        IGameRepository            games,
        IPlatformConnectorRegistry registry)
    {
        _users    = users;
        _games    = games;
        _registry = registry;
    }

    public async Task<IReadOnlyList<GameDto>> SyncAndGetLibraryAsync(
        Guid              userId,
        CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId, ct)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        var isStale = !user.Games.Any()
            || (DateTimeOffset.UtcNow - user.LastSyncedAt).TotalHours >= StalenessHours;

        Dictionary<Guid, Game> gamesById;

        if (isStale)
            gamesById = await SyncFromSteamAsync(user, ct);
        else
        {
            var ids = user.Games.Select(ug => ug.GameId).ToList();
            gamesById = await _games.FindByIdsAsync(ids, ct);
        }

        return user.Games
            .Where(ug => gamesById.ContainsKey(ug.GameId))
            .Select(ug =>
            {
                var g = gamesById[ug.GameId];
                return new GameDto(
                    g.Id, g.AppId.Value, g.Name, g.CoverImageUrl, ug.PlaytimeMinutes,
                    g.Tags, g.Screenshots, g.ReleaseDate, g.ReviewSummary, g.ReviewScore);
            })
            .OrderByDescending(d => d.PlaytimeMinutes)
            .ToList();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private async Task<Dictionary<Guid, Game>> SyncFromSteamAsync(User user, CancellationToken ct)
    {
        var connector     = _registry.Get(Platform.Steam);
        var platformGames = await connector.GetOwnedGamesAsync(user.SteamId.Value.ToString(), ct: ct);

        var appIds        = platformGames.Select(g => long.Parse(g.PlatformGameId)).ToList();
        var existingGames = await _games.FindByAppIdsAsync(appIds, ct);

        var newGames = platformGames
            .Where(g => !existingGames.ContainsKey(long.Parse(g.PlatformGameId)))
            .Select(g => Game.Create(new AppId(long.Parse(g.PlatformGameId)), g.Name, Platform.Steam))
            .ToList();

        if (newGames.Count > 0)
        {
            await _games.AddRangeAsync(newGames, ct);
            await _games.SaveChangesAsync(ct);
            foreach (var g in newGames)
                existingGames[g.AppId.Value] = g;
        }

        var existingUserGames = user.Games.ToDictionary(ug => ug.GameId);

        foreach (var pg in platformGames)
        {
            if (!existingGames.TryGetValue(long.Parse(pg.PlatformGameId), out var game)) continue;

            if (existingUserGames.TryGetValue(game.Id, out var userGame))
                userGame.UpdatePlaytime(pg.PlaytimeMinutes);
            else
                user.AddGame(UserGame.Create(user.Id, game.Id, pg.PlaytimeMinutes));
        }

        user.MarkLibrarySynced();
        await _users.SaveChangesAsync(ct);

        return existingGames.Values.ToDictionary(g => g.Id);
    }
}
