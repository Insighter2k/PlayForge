namespace PlayForge.Domain.Entities;

public class Vote
{
    public Guid          SessionId { get; private set; }
    public Guid          UserId    { get; private set; }
    public Guid          GameId    { get; private set; }
    public DateTimeOffset CastAt   { get; private set; }

    // EF constructor
    private Vote() { }

    public static Vote Create(Guid sessionId, Guid userId, Guid gameId)
    {
        return new Vote
        {
            SessionId = sessionId,
            UserId    = userId,
            GameId    = gameId,
            CastAt    = DateTimeOffset.UtcNow,
        };
    }
}
