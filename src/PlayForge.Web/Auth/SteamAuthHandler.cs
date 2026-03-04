using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using PlayForge.Domain.Entities;
using PlayForge.Domain.Interfaces;
using PlayForge.Domain.ValueObjects;

namespace PlayForge.Web.Auth;

/// <summary>
/// After Steam OpenID authentication, syncs or creates the local user record
/// and adds a PlayForgeUserId claim to the principal.
/// </summary>
public class SteamAuthHandler : IClaimsTransformation
{
    // Steam OpenID nameidentifier is the full URL:
    // https://steamcommunity.com/openid/id/76561198XXXXXXXXX
    private const string SteamOpenIdPrefix = "https://steamcommunity.com/openid/id/";
    public const  string PlayForgeUserIdClaim = "playforge_user_id";

    private readonly IUserRepository    _users;
    private readonly IPlatformConnector _steam;
    private readonly ILogger<SteamAuthHandler> _logger;

    public SteamAuthHandler(
        IUserRepository    users,
        IPlatformConnector steam,
        ILogger<SteamAuthHandler> logger)
    {
        _users  = users;
        _steam  = steam;
        _logger = logger;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // Only run for authenticated Steam users that don't already have our claim
        if (!principal.Identity?.IsAuthenticated ?? true) return principal;
        if (principal.HasClaim(c => c.Type == PlayForgeUserIdClaim)) return principal;

        var rawClaimValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (rawClaimValue is null || !rawClaimValue.StartsWith(SteamOpenIdPrefix))
            return principal;

        var steamIdRaw = rawClaimValue[SteamOpenIdPrefix.Length..];
        if (!long.TryParse(steamIdRaw, out var steamIdValue)) return principal;

        var steamId = new SteamId(steamIdValue);

        try
        {
            var user = await _users.FindBySteamIdAsync(steamId);

            if (user is null)
            {
                var profile = await _steam.GetUserProfileAsync(steamIdRaw);
                user = User.Create(steamId, profile.DisplayName, profile.AvatarUrl);
                await _users.AddAsync(user);
                await _users.SaveChangesAsync();
                _logger.LogInformation("New PlayForge user created: {SteamId}", steamIdRaw);
            }

            var identity = new ClaimsIdentity();
            identity.AddClaim(new Claim(PlayForgeUserIdClaim, user.Id.ToString()));
            identity.AddClaim(new Claim(ClaimTypes.Name,      user.DisplayName));
            identity.AddClaim(new Claim("avatar_url",         user.AvatarUrl));
            principal.AddIdentity(identity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync Steam user {SteamId}", steamIdRaw);
        }

        return principal;
    }
}
