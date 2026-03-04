using PlayForge.Domain.Enums;
using PlayForge.Domain.Interfaces;

namespace PlayForge.Infrastructure.Steam;

public class SteamPlatformConnector : IPlatformConnector
{
    private readonly SteamApiClient   _client;
    private readonly SteamStoreClient _store;

    public Platform Platform => Platform.Steam;

    public SteamPlatformConnector(SteamApiClient client, SteamStoreClient store)
    {
        _client = client;
        _store  = store;
    }

    public async Task<PlatformUserProfile> GetUserProfileAsync(
        string            platformUserId,
        CancellationToken ct = default)
    {
        var players = await _client.GetPlayerSummariesAsync([platformUserId], ct);
        var player  = players.FirstOrDefault()
            ?? throw new InvalidOperationException($"Steam user {platformUserId} not found.");

        return new PlatformUserProfile(
            player.SteamId,
            Platform.Steam,
            player.PersonaName,
            player.AvatarFull);
    }

    public async Task<IReadOnlyList<PlatformGame>> GetOwnedGamesAsync(
        string            platformUserId,
        bool              includeFreeToPlay = false,
        CancellationToken ct               = default)
    {
        var games = await _client.GetOwnedGamesAsync(platformUserId, ct);
        return games
            .Select(g => new PlatformGame(
                g.AppId.ToString(),
                Platform.Steam,
                g.Name,
                $"https://cdn.akamai.steamstatic.com/steam/apps/{g.AppId}/header.jpg",
                g.PlaytimeForever))
            .ToList();
    }

    public async Task<IReadOnlyList<PlatformFriend>> GetFriendsAsync(
        string            platformUserId,
        CancellationToken ct = default)
    {
        var friends = await _client.GetFriendListAsync(platformUserId, ct);
        if (friends.Count == 0) return [];

        // Batch-fetch profiles
        var profiles = await _client.GetPlayerSummariesAsync(friends.Select(f => f.SteamId), ct);
        return profiles
            .Select(p => new PlatformFriend(p.SteamId, Platform.Steam, p.PersonaName, p.AvatarFull))
            .ToList();
    }

    public async Task<IReadOnlyList<SearchGameResult>> SearchGamesAsync(
        string            query,
        int               limit = 10,
        CancellationToken ct    = default)
    {
        var results = await _store.SearchAsync(query, limit, ct);
        return results
            .Select(r => new SearchGameResult(r.AppId, r.Name, r.CoverImageUrl))
            .ToList();
    }
}
