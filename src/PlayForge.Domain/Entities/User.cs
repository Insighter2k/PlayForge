using PlayForge.Domain.ValueObjects;

namespace PlayForge.Domain.Entities;

public class User
{
    public Guid    Id          { get; private set; }
    public SteamId SteamId     { get; private set; } = null!;
    public string  DisplayName { get; private set; } = string.Empty;
    public string  AvatarUrl   { get; private set; } = string.Empty;
    public DateTimeOffset LastSyncedAt { get; private set; }

    private readonly List<GameGroup>       _groups      = [];
    private readonly List<UserGame>        _games       = [];

    public IReadOnlyList<UserGame>   Games  => _games.AsReadOnly();
    public IReadOnlyList<GameGroup>  Groups => _groups.AsReadOnly();

    // EF constructor
    private User() { }

    public static User Create(SteamId steamId, string displayName, string avatarUrl)
    {
        return new User
        {
            Id          = Guid.NewGuid(),
            SteamId     = steamId,
            DisplayName = displayName,
            AvatarUrl   = avatarUrl,
            LastSyncedAt = DateTimeOffset.UtcNow,
        };
    }

    public void UpdateProfile(string displayName, string avatarUrl)
    {
        DisplayName  = displayName;
        AvatarUrl    = avatarUrl;
        LastSyncedAt = DateTimeOffset.UtcNow;
    }

    public void AddGame(UserGame game)  => _games.Add(game);
    public void MarkLibrarySynced()     => LastSyncedAt = DateTimeOffset.UtcNow;
}
