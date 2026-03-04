namespace PlayForge.Application.DTOs;

public record FriendDto(
    string SteamId,
    string DisplayName,
    string AvatarUrl,
    bool   IsRegistered,
    Guid?  UserId
);
