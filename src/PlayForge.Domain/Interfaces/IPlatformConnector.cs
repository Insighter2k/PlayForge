using PlayForge.Domain.Enums;

namespace PlayForge.Domain.Interfaces;

// Result records — not domain entities; just data transfer from external platforms
public record PlatformUserProfile(string PlatformUserId, Platform Platform, string DisplayName, string AvatarUrl);
public record PlatformGame(string PlatformGameId, Platform Platform, string Name, string CoverImageUrl, int PlaytimeMinutes);
public record PlatformFriend(string PlatformUserId, Platform Platform, string DisplayName, string AvatarUrl);
public record SearchGameResult(long AppId, string Name, string CoverImageUrl);

/// <summary>
/// Core extensibility contract. To add Epic/GOG: implement this interface + register in DI.
/// Zero application-layer changes required.
/// </summary>
public interface IPlatformConnector
{
    Platform Platform { get; }

    Task<PlatformUserProfile> GetUserProfileAsync(string platformUserId, CancellationToken ct = default);

    Task<IReadOnlyList<PlatformGame>> GetOwnedGamesAsync(
        string platformUserId,
        bool   includeFreeToPlay = false,
        CancellationToken ct     = default);

    Task<IReadOnlyList<PlatformFriend>> GetFriendsAsync(string platformUserId, CancellationToken ct = default);

    // Default: platforms that don't support search return empty
    Task<IReadOnlyList<SearchGameResult>> SearchGamesAsync(
        string            query,
        int               limit = 10,
        CancellationToken ct    = default)
        => Task.FromResult<IReadOnlyList<SearchGameResult>>([]);
}
