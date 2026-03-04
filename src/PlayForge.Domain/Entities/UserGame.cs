namespace PlayForge.Domain.Entities;

// Explicit join entity — holds playtime from Steam API
public class UserGame
{
    public Guid UserId          { get; private set; }
    public Guid GameId          { get; private set; }
    public int  PlaytimeMinutes { get; private set; }
    public DateTimeOffset SyncedAt { get; private set; }

    public User? User { get; private set; }
    public Game? Game { get; private set; }

    // EF constructor
    private UserGame() { }

    public static UserGame Create(Guid userId, Guid gameId, int playtimeMinutes)
    {
        return new UserGame
        {
            UserId          = userId,
            GameId          = gameId,
            PlaytimeMinutes = playtimeMinutes,
            SyncedAt        = DateTimeOffset.UtcNow,
        };
    }

    public void UpdatePlaytime(int minutes)
    {
        PlaytimeMinutes = minutes;
        SyncedAt        = DateTimeOffset.UtcNow;
    }
}
