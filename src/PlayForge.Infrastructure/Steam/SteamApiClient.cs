using Microsoft.Extensions.Options;
using PlayForge.Infrastructure.Steam.Models;
using PlayForge.Infrastructure.Steam.Options;
using System.Net.Http.Json;

namespace PlayForge.Infrastructure.Steam;

public class SteamApiClient
{
    private readonly HttpClient       _http;
    private readonly SteamApiOptions  _options;

    public SteamApiClient(HttpClient http, IOptions<SteamApiOptions> options)
    {
        _http    = http;
        _options = options.Value;
        _http.BaseAddress = new Uri(_options.BaseUrl);
    }

    public async Task<List<SteamPlayer>> GetPlayerSummariesAsync(
        IEnumerable<string> steamIds,
        CancellationToken   ct = default)
    {
        // Steam API allows up to 100 IDs per call
        var ids = steamIds.Take(100);
        var url = $"ISteamUser/GetPlayerSummaries/v2/?key={_options.ApiKey}&steamids={string.Join(',', ids)}";
        var response = await _http.GetFromJsonAsync<PlayerSummariesResponse>(url, ct);
        return response?.Response.Players ?? [];
    }

    public async Task<List<SteamOwnedGame>> GetOwnedGamesAsync(
        string            steamId,
        CancellationToken ct = default)
    {
        var url = $"IPlayerService/GetOwnedGames/v1/?key={_options.ApiKey}&steamid={steamId}&include_appinfo=1&format=json";
        var response = await _http.GetFromJsonAsync<OwnedGamesResponse>(url, ct);
        return response?.Response.Games ?? [];
    }

    public async Task<List<SteamFriend>> GetFriendListAsync(
        string            steamId,
        CancellationToken ct = default)
    {
        var url      = $"ISteamUser/GetFriendList/v1/?key={_options.ApiKey}&steamid={steamId}&relationship=friend";
        var response = await _http.GetAsync(url, ct);

        // 401 = friend list is private on the user's Steam profile
        if (!response.IsSuccessStatusCode) return [];

        var result = await response.Content.ReadFromJsonAsync<FriendListResponse>(ct);
        return result?.FriendsList.Friends ?? [];
    }
}
