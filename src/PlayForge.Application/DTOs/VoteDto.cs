using PlayForge.Domain.Enums;

namespace PlayForge.Application.DTOs;

public record GameCandidateDto(
    Guid     GameId,
    long     AppId,
    string   Name,
    string   CoverImageUrl,
    string[] Tags,
    string[] Screenshots,
    string?  ReleaseDate,
    string?  ReviewSummary,
    int      ReviewScore
);

public record VoteSessionDto(
    Guid             Id,
    Guid             GroupId,
    VoteSessionStatus Status,
    Guid[]           CandidateGameIds,
    Guid?            WinnerGameId,
    DateTimeOffset   StartedAt,
    DateTimeOffset?  ClosedAt
);

public record VoteTallyDto(
    Guid                        SessionId,
    IReadOnlyDictionary<Guid, int> Tally,
    int                         TotalVotes
);

public record VoteHistoryEntryDto(
    Guid            SessionId,
    DateTimeOffset  StartedAt,
    DateTimeOffset? ClosedAt,
    int             TotalVotes,
    string?         WinnerName,
    string?         WinnerCoverImageUrl
);
