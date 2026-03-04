using System.Text.Json.Serialization;

namespace PlayForge.Infrastructure.Steam.Models;

// GetPlayerSummaries/v2
public record PlayerSummariesResponse(
    [property: JsonPropertyName("response")] PlayerSummariesBody Response
);
public record PlayerSummariesBody(
    [property: JsonPropertyName("players")] List<SteamPlayer> Players
);
public record SteamPlayer(
    [property: JsonPropertyName("steamid")]    string SteamId,
    [property: JsonPropertyName("personaname")] string PersonaName,
    [property: JsonPropertyName("avatarfull")] string AvatarFull
);

// GetOwnedGames/v1
public record OwnedGamesResponse(
    [property: JsonPropertyName("response")] OwnedGamesBody Response
);
public record OwnedGamesBody(
    [property: JsonPropertyName("game_count")]  int GameCount,
    [property: JsonPropertyName("games")]       List<SteamOwnedGame>? Games
);
public record SteamOwnedGame(
    [property: JsonPropertyName("appid")]              long AppId,
    [property: JsonPropertyName("name")]               string Name,
    [property: JsonPropertyName("playtime_forever")]   int PlaytimeForever
);

// GetFriendList/v1
public record FriendListResponse(
    [property: JsonPropertyName("friendslist")] FriendListBody FriendsList
);
public record FriendListBody(
    [property: JsonPropertyName("friends")] List<SteamFriend> Friends
);
public record SteamFriend(
    [property: JsonPropertyName("steamid")]       string SteamId,
    [property: JsonPropertyName("friend_since")]  long FriendSince
);
