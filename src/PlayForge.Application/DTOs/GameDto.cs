namespace PlayForge.Application.DTOs;

public record GameDto(
    Guid     Id,
    long     AppId,
    string   Name,
    string   CoverImageUrl,
    int      PlaytimeMinutes,
    string[] Tags,
    string[] Screenshots,
    string?  ReleaseDate,
    string?  ReviewSummary,
    int      ReviewScore
);
