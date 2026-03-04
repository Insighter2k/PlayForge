using PlayForge.Application.DTOs;
using PlayForge.Application.Interfaces;
using PlayForge.Domain.Enums;
using PlayForge.Domain.Interfaces;

namespace PlayForge.Application.Services;

public class FriendService : IFriendService
{
    private readonly IUserRepository            _users;
    private readonly IPlatformConnectorRegistry _registry;

    public FriendService(IUserRepository users, IPlatformConnectorRegistry registry)
    {
        _users    = users;
        _registry = registry;
    }

    public async Task<IReadOnlyList<FriendDto>> GetFriendsAsync(
        Guid              userId,
        CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId, ct)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        var connector       = _registry.Get(Platform.Steam);
        var platformFriends = await connector.GetFriendsAsync(user.SteamId.Value.ToString(), ct);

        if (platformFriends.Count == 0) return [];

        var friendSteamIds  = platformFriends.Select(f => long.Parse(f.PlatformUserId)).ToList();
        var registeredUsers = await _users.FindBySteamIdsAsync(friendSteamIds, ct);

        return platformFriends
            .Select(f =>
            {
                var steamIdLong  = long.Parse(f.PlatformUserId);
                var isRegistered = registeredUsers.TryGetValue(steamIdLong, out var regUser);
                return new FriendDto(f.PlatformUserId, f.DisplayName, f.AvatarUrl, isRegistered, regUser?.Id);
            })
            .OrderByDescending(f => f.IsRegistered)
            .ThenBy(f => f.DisplayName)
            .ToList();
    }
}
