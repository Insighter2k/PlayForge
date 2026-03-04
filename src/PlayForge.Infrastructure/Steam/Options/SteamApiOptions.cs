namespace PlayForge.Infrastructure.Steam.Options;

public class SteamApiOptions
{
    public const string Section = "Steam";

    public string ApiKey  { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.steampowered.com";

    /// <summary>Library cache TTL in minutes.</summary>
    public int LibraryCacheMinutes { get; set; } = 60;

    /// <summary>Friend list cache TTL in minutes.</summary>
    public int FriendCacheMinutes { get; set; } = 30;

    /// <summary>Common-games cache TTL in minutes.</summary>
    public int CommonGamesCacheMinutes { get; set; } = 15;
}
