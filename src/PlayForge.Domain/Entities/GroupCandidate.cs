namespace PlayForge.Domain.Entities;

public class GroupCandidate
{
    public Guid   Id            { get; private set; }
    public Guid   GroupId       { get; private set; }
    public Guid   AddedByUserId { get; private set; }
    public long   AppId         { get; private set; }
    public string Name          { get; private set; } = string.Empty;
    public string CoverImageUrl { get; private set; } = string.Empty;
    public DateTimeOffset AddedAt { get; private set; }

    // EF navigation
    public GameGroup? Group { get; private set; }

    // EF constructor
    private GroupCandidate() { }

    public static GroupCandidate Create(
        Guid   groupId,
        Guid   addedByUserId,
        long   appId,
        string name,
        string coverImageUrl) =>
        new()
        {
            Id            = Guid.NewGuid(),
            GroupId       = groupId,
            AddedByUserId = addedByUserId,
            AppId         = appId,
            Name          = name,
            CoverImageUrl = coverImageUrl,
            AddedAt       = DateTimeOffset.UtcNow,
        };
}
