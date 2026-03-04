using PlayForge.Domain.Enums;
using PlayForge.Domain.ValueObjects;

namespace PlayForge.Domain.Entities;

public class Game
{
    public Guid     Id            { get; private set; }
    public AppId    AppId         { get; private set; } = null!;
    public string   Name          { get; private set; } = string.Empty;
    public string   CoverImageUrl { get; private set; } = string.Empty;
    public Platform Platform      { get; private set; }
    public string[] Tags          { get; private set; } = [];
    public string[] Screenshots   { get; private set; } = [];
    public string?  ReleaseDate   { get; private set; }
    public string?  ReviewSummary { get; private set; }
    public int      ReviewScore   { get; private set; }

    // EF constructor
    private Game() { }

    public static Game Create(AppId appId, string name, Platform platform)
    {
        return new Game
        {
            Id            = Guid.NewGuid(),
            AppId         = appId,
            Name          = name,
            Platform      = platform,
            CoverImageUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{appId.Value}/header.jpg",
        };
    }

    public void UpdateName(string name) => Name = name;

    public void UpdateStoreData(
        string[] tags,
        string[] screenshots,
        string?  releaseDate,
        string?  reviewSummary,
        int      reviewScore)
    {
        Tags          = tags;
        Screenshots   = screenshots;
        ReleaseDate   = releaseDate;
        ReviewSummary = reviewSummary;
        ReviewScore   = reviewScore;
    }
}
