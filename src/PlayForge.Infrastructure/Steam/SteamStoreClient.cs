using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlayForge.Infrastructure.Steam;

// ── Typed response models (search only) ──────────────────────────────────────

file record StoreSearchResponse(
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("items")] List<StoreSearchItem> Items
);

file record StoreSearchItem(
    [property: JsonPropertyName("id")]   long   Id,
    [property: JsonPropertyName("name")] string Name
);

// Categories that describe Steam infrastructure rather than gameplay
file static class CategoryBlocklist
{
    internal static readonly HashSet<string> Values = new(StringComparer.OrdinalIgnoreCase)
    {
        "Steam Achievements", "Steam Cloud", "Steam Trading Cards",
        "Steam Workshop", "Steam Leaderboards", "SteamVR Collectibles",
        "Valve Anti-Cheat enabled", "In-App Purchases",
        "Captions available", "Commentary available",
        "Includes Source SDK", "Includes level editor",
        "Remote Play on Phone", "Remote Play on Tablet", "Remote Play on TV",
        "Stats",
    };
}

// ── Client ────────────────────────────────────────────────────────────────────

public class SteamStoreClient
{
    private readonly HttpClient _http;

    public SteamStoreClient(HttpClient http) => _http = http;

    // ── Store search ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<(long AppId, string Name, string CoverImageUrl)>> SearchAsync(
        string            query,
        int               limit = 10,
        CancellationToken ct    = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var url = "https://store.steampowered.com/api/storesearch/"
                + $"?term={Uri.EscapeDataString(query.Trim())}&cc=us&l=english&category1=998";

        var response = await _http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return [];

        var result = await response.Content.ReadFromJsonAsync<StoreSearchResponse>(ct);
        if (result is null) return [];

        return result.Items
            .Take(limit)
            .Select(i => (
                i.Id,
                i.Name,
                $"https://cdn.akamai.steamstatic.com/steam/apps/{i.Id}/header.jpg"))
            .ToList();
    }

    // ── Hover-card preview ────────────────────────────────────────────────────

    /// <summary>
    /// Fetches full preview data for the hover card.
    /// Uses JsonDocument to avoid typed-record deserialization edge-cases with
    /// Steam's inconsistent response shapes (tags as dict vs array, etc.).
    /// </summary>
    public async Task<GamePreviewResult?> GetPreviewDataAsync(long appId, CancellationToken ct = default)
    {
        try
        {
            // filters=basic gives us name + is_free
            const string filters = "basic,categories,genres,tags,screenshots,release_date,price_overview,movies";
            var detailsUrl = $"https://store.steampowered.com/api/appdetails?appids={appId}&filters={filters}&cc=us&l=en";
            var reviewsUrl = $"https://store.steampowered.com/appreviews/{appId}?json=1&num_per_page=0&language=all";

            // Fire both in parallel — use ConfigureAwait(false) to avoid deadlocks
            var detailsTask = _http.GetAsync(detailsUrl, ct);
            var reviewsTask = _http.GetAsync(reviewsUrl, ct);
            await Task.WhenAll(detailsTask, reviewsTask);

            if (!detailsTask.Result.IsSuccessStatusCode) return null;

            var detailsJson = await detailsTask.Result.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(detailsJson)) return null;

            using var doc = JsonDocument.Parse(detailsJson);

            // Root is { "570": { "success": bool, "data": { ... } } }
            if (!doc.RootElement.TryGetProperty(appId.ToString(), out var appEl)) return null;
            if (!appEl.TryGetProperty("success", out var successEl) || !successEl.GetBoolean()) return null;
            if (!appEl.TryGetProperty("data", out var data)) return null;

            var name   = GetString(data, "name") ?? string.Empty;
            var isFree = GetBool(data, "is_free");

            var tags        = ExtractTags(data);
            var genres      = ExtractDescriptions(data, "genres");
            var categories  = ExtractDescriptions(data, "categories")
                                .Where(c => !CategoryBlocklist.Values.Contains(c))
                                .ToArray();
            var screenshots = ExtractScreenshots(data);
            var trailerUrl  = ExtractTrailerUrl(data);
            var releaseDate = ExtractReleaseDate(data);

            var (price, priceOriginal, discountPercent) = ExtractPrice(data, isFree);

            // Reviews — parse from separate response
            string? reviewSummary = null;
            int     reviewScore   = 0;

            if (reviewsTask.Result.IsSuccessStatusCode)
            {
                var reviewsJson = await reviewsTask.Result.Content.ReadAsStringAsync(ct);
                if (!string.IsNullOrWhiteSpace(reviewsJson))
                {
                    using var rdoc = JsonDocument.Parse(reviewsJson);
                    if (rdoc.RootElement.TryGetProperty("query_summary", out var qs))
                    {
                        var total = GetInt(qs, "total_reviews");
                        if (total > 0)
                        {
                            reviewSummary = GetString(qs, "review_score_desc");
                            reviewScore   = GetInt(qs, "review_score");
                        }
                    }
                }
            }

            return new GamePreviewResult(
                Name:            name,
                Tags:            tags,
                Genres:          genres,
                Categories:      categories,
                Screenshots:     screenshots,
                TrailerUrl:      trailerUrl,
                ReleaseDate:     releaseDate,
                ReviewSummary:   reviewSummary,
                ReviewScore:     reviewScore,
                IsFree:          isFree,
                Price:           price,
                PriceOriginal:   priceOriginal,
                DiscountPercent: discountPercent);
        }
        catch
        {
            return null;
        }
    }

    // ── JsonDocument helpers ──────────────────────────────────────────────────

    private static string? GetString(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    private static bool GetBool(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;

    private static int GetInt(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.TryGetInt32(out var i) ? i : 0;

    private static string[] ExtractDescriptions(JsonElement data, string key)
    {
        if (!data.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];

        return arr.EnumerateArray()
            .Select(e => GetString(e, "description"))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Cast<string>()
            .Select(s => s.Trim())
            .ToArray();
    }

    /// <summary>
    /// Steam returns "tags" as one of:
    ///   • array  [{id, description}, ...]
    ///   • object {"id": "name", ...}
    /// Falls back to genres + filtered categories when absent.
    /// </summary>
    private static string[] ExtractTags(JsonElement data)
    {
        if (data.TryGetProperty("tags", out var tags))
        {
            if (tags.ValueKind == JsonValueKind.Array)
            {
                var arr = tags.EnumerateArray()
                    .Select(t =>
                        GetString(t, "description")
                        ?? GetString(t, "name"))
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Cast<string>()
                    .Select(s => s.Trim())
                    .ToArray();
                if (arr.Length > 0) return arr;
            }
            else if (tags.ValueKind == JsonValueKind.Object)
            {
                var dict = tags.EnumerateObject()
                    .Select(p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : null)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Cast<string>()
                    .Select(s => s.Trim())
                    .ToArray();
                if (dict.Length > 0) return dict;
            }
        }

        // Fallback: genres + filtered categories
        var genres = ExtractDescriptions(data, "genres");
        var cats   = ExtractDescriptions(data, "categories")
                       .Where(c => !CategoryBlocklist.Values.Contains(c));
        return genres.Concat(cats).Distinct().ToArray();
    }

    private static string[] ExtractScreenshots(JsonElement data)
    {
        if (!data.TryGetProperty("screenshots", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];

        return arr.EnumerateArray()
            .Select(e => GetString(e, "path_full"))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Cast<string>()
            .Take(6)
            .ToArray();
    }

    private static string? ExtractTrailerUrl(JsonElement data)
    {
        if (!data.TryGetProperty("movies", out var movies) || movies.ValueKind != JsonValueKind.Array)
            return null;

        // Prefer a highlighted trailer; fall back to the first available
        JsonElement? highlighted = null;
        JsonElement? first       = null;

        foreach (var movie in movies.EnumerateArray())
        {
            first ??= movie;
            if (GetBool(movie, "highlight")) { highlighted = movie; break; }
        }

        var candidate = highlighted ?? first;
        if (candidate is null) return null;

        // Prefer MP4 480p for compatibility
        if (candidate.Value.TryGetProperty("mp4", out var mp4))
        {
            return GetString(mp4, "480") ?? GetString(mp4, "max");
        }
        if (candidate.Value.TryGetProperty("webm", out var webm))
        {
            return GetString(webm, "480") ?? GetString(webm, "max");
        }
        return null;
    }

    private static string? ExtractReleaseDate(JsonElement data)
    {
        if (!data.TryGetProperty("release_date", out var rd)) return null;
        if (GetBool(rd, "coming_soon")) return null;
        var date = GetString(rd, "date");
        return string.IsNullOrWhiteSpace(date) ? null : date;
    }

    private static (string? Price, string? PriceOriginal, int Discount) ExtractPrice(
        JsonElement data, bool isFree)
    {
        if (data.TryGetProperty("price_overview", out var po))
        {
            var final    = GetString(po, "final_formatted");
            var discount = GetInt(po, "discount_percent");
            var original = discount > 0 ? GetString(po, "initial_formatted") : null;
            return (final, original, discount);
        }

        return isFree ? ("Free to Play", null, 0) : (null, null, 0);
    }
}
