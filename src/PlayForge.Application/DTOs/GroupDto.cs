namespace PlayForge.Application.DTOs;

public record GroupDto(
    Guid   Id,
    string Name,
    string InviteCode,
    Guid   HostUserId,
    int    MemberCount
);

public record GroupSummaryDto(
    Guid   Id,
    string Name,
    int    MemberCount,
    bool   HasActiveSession
);

public record GameSearchResultDto(
    long   AppId,
    string Name,
    string CoverImageUrl
);

public record GroupCandidateDto(
    Guid   Id,
    Guid   AddedByUserId,
    long   AppId,
    string Name,
    string CoverImageUrl
);
