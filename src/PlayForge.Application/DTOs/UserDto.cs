namespace PlayForge.Application.DTOs;

public record UserDto(
    Guid   Id,
    string SteamId,
    string DisplayName,
    string AvatarUrl
);
