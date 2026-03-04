using Microsoft.AspNetCore.Mvc;
using PlayForge.Domain.Interfaces;
using PlayForge.Domain.ValueObjects;
using PlayForge.Infrastructure.Steam;

namespace PlayForge.Web.Controllers;

[ApiController]
[Route("api/game-preview")]
public class GamePreviewController : ControllerBase
{
    private readonly SteamStoreClient  _store;
    private readonly IGameRepository   _games;

    public GamePreviewController(SteamStoreClient store, IGameRepository games)
    {
        _store = store;
        _games = games;
    }

    [HttpGet("{appId:long}")]
    public async Task<IActionResult> Get(long appId, CancellationToken ct)
    {
        var result = await _store.GetPreviewDataAsync(appId, ct);
        if (result is null) return NoContent();

        // Lazily store tags in the DB so the tag filter works without bulk enrichment
        if (result.Tags.Length > 0)
        {
            var gameMap = await _games.FindByAppIdsAsync([appId], ct);
            if (gameMap.TryGetValue(appId, out var game) && game.Tags.Length == 0)
            {
                game.UpdateStoreData(
                    result.Tags,
                    result.Screenshots,
                    result.ReleaseDate,
                    result.ReviewSummary,
                    result.ReviewScore);
                await _games.SaveChangesAsync(ct);
            }
        }

        return Ok(result);
    }
}
