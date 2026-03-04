namespace PlayForge.Infrastructure.Steam;

/// <summary>
/// Rich preview data returned by the hover-card API endpoint.
/// </summary>
public record GamePreviewResult(
    string   Name,
    string[] Tags,
    string[] Genres,
    string[] Categories,
    string[] Screenshots,
    string?  TrailerUrl,
    string?  ReleaseDate,
    string?  ReviewSummary,
    int      ReviewScore,
    bool     IsFree,
    string?  Price,
    string?  PriceOriginal,
    int      DiscountPercent
);
