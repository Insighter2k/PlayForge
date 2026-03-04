using PlayForge.Application.DTOs;
using PlayForge.Application.Interfaces;
using PlayForge.Domain.Entities;
using PlayForge.Domain.Enums;
using PlayForge.Domain.Exceptions;
using PlayForge.Domain.Interfaces;
using PlayForge.Domain.ValueObjects;

namespace PlayForge.Application.Services;

public class VoteService : IVoteService
{
    private readonly IVoteSessionRepository _sessions;
    private readonly IGroupRepository       _groups;
    private readonly IUserRepository        _users;
    private readonly IGameRepository        _games;

    public VoteService(
        IVoteSessionRepository sessions,
        IGroupRepository       groups,
        IUserRepository        users,
        IGameRepository        games)
    {
        _sessions = sessions;
        _groups   = groups;
        _users    = users;
        _games    = games;
    }

    // ── Start ─────────────────────────────────────────────────────────────────

    public async Task<VoteSessionDto> StartSessionAsync(
        Guid              groupId,
        Guid              hostUserId,
        CancellationToken ct = default)
    {
        var group = await _groups.FindByIdAsync(groupId, ct)
            ?? throw new InvalidOperationException("Group not found.");

        if (group.HostUserId != hostUserId)
            throw new DomainException("Only the group host can start a vote.");

        if (await _sessions.FindActiveByGroupIdAsync(groupId, ct) is not null)
            throw new DomainException("A vote session is already active for this group.");

        // ── Common games (library intersection) ──────────────────────────────
        var memberIds        = group.Memberships.Select(m => m.UserId).ToList();
        var members          = await _users.FindByIdsAsync(memberIds, ct);
        var membersWithGames = members.Where(u => u.Games.Any()).ToList();

        var commonGameIds = membersWithGames.Count > 0
            ? membersWithGames
                .Select(u => u.Games.Select(ug => ug.GameId).ToHashSet())
                .Aggregate((a, b) => { a.IntersectWith(b); return a; })
            : [];

        // ── Group candidates — upsert to games table so they get a stable Guid ──
        var candidateAppIds = group.Candidates.Select(c => c.AppId).ToList();
        var existingByAppId = candidateAppIds.Count > 0
            ? await _games.FindByAppIdsAsync(candidateAppIds, ct)
            : new Dictionary<long, Game>();

        // Create Game rows for candidates not yet in the catalogue
        var newCatalogueGames = group.Candidates
            .Where(c => !existingByAppId.ContainsKey(c.AppId))
            .Select(c => Game.Create(new AppId(c.AppId), c.Name, Platform.Steam))
            .ToList();

        if (newCatalogueGames.Count > 0)
            await _games.AddRangeAsync(newCatalogueGames, ct);

        var candidateGameIds = existingByAppId.Values
            .Select(g => g.Id)
            .Concat(newCatalogueGames.Select(g => g.Id));

        // ── Build full ballot (common ∪ candidates) ───────────────────────────
        var ballotIds = commonGameIds.Union(candidateGameIds).ToHashSet();

        if (ballotIds.Count == 0)
            throw new DomainException(
                "No vote candidates found — sync your library or add candidates to the group first.");

        var session = VoteSession.Create(groupId, ballotIds);
        await _sessions.AddAsync(session, ct);
        await _sessions.SaveChangesAsync(ct); // saves new Game rows + VoteSession atomically

        return ToDto(session);
    }

    // ── History ──────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<VoteHistoryEntryDto>> GetGroupHistoryAsync(
        Guid groupId, CancellationToken ct = default)
    {
        var sessions = await _sessions.FindClosedByGroupIdAsync(groupId, ct);
        if (sessions.Count == 0) return [];

        var winnerIds = sessions
            .Where(s => s.WinnerGameId.HasValue)
            .Select(s => s.WinnerGameId!.Value)
            .Distinct()
            .ToList();

        var gamesById = winnerIds.Count > 0
            ? await _games.FindByIdsAsync(winnerIds, ct)
            : new Dictionary<Guid, Domain.Entities.Game>();

        return sessions.Select(s =>
        {
            var totalVotes = s.Votes.Count;
            string? winnerName  = null;
            string? winnerCover = null;
            if (s.WinnerGameId.HasValue && gamesById.TryGetValue(s.WinnerGameId.Value, out var g))
            {
                winnerName  = g.Name;
                winnerCover = g.CoverImageUrl;
            }
            return new VoteHistoryEntryDto(s.Id, s.StartedAt, s.ClosedAt, totalVotes, winnerName, winnerCover);
        }).ToList();
    }

    // ── Query ────────────────────────────────────────────────────────────────

    public async Task<VoteSessionDto?> GetActiveSessionAsync(Guid groupId, CancellationToken ct = default)
    {
        var session = await _sessions.FindActiveByGroupIdAsync(groupId, ct);
        return session is null ? null : ToDto(session);
    }

    public async Task<IReadOnlyList<GameCandidateDto>> GetSessionCandidatesAsync(
        Guid              sessionId,
        CancellationToken ct = default)
    {
        var session = await _sessions.FindByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException("Vote session not found.");

        var gamesById = await _games.FindByIdsAsync(session.CandidateGameIds, ct);

        return gamesById.Values
            .Select(g => new GameCandidateDto(
                g.Id, g.AppId.Value, g.Name, g.CoverImageUrl, g.Tags,
                g.Screenshots, g.ReleaseDate, g.ReviewSummary, g.ReviewScore))
            .OrderBy(g => g.Name)
            .ToList();
    }

    public async Task<VoteTallyDto> GetSessionTallyAsync(Guid sessionId, CancellationToken ct = default)
    {
        var tally = await _sessions.GetTallyAsync(sessionId, ct);
        return new VoteTallyDto(sessionId, tally, tally.Values.Sum());
    }

    public Task<IReadOnlyList<Guid>> GetUserVotesAsync(Guid sessionId, Guid userId, CancellationToken ct = default)
        => _sessions.GetUserVotesAsync(sessionId, userId, ct);

    // ── Cast vote ─────────────────────────────────────────────────────────────

    public async Task<VoteTallyDto> CastVoteAsync(
        Guid              sessionId,
        Guid              userId,
        Guid              gameId,
        CancellationToken ct = default)
    {
        var session = await _sessions.FindByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException("Vote session not found.");

        if (session.Status != VoteSessionStatus.Active)
            throw new DomainException("This vote session is no longer active.");

        if (!session.CandidateGameIds.Contains(gameId))
            throw new DomainException("That game is not a candidate in this vote session.");

        await _sessions.ToggleVoteAsync(sessionId, userId, gameId, maxVotes: 5, ct);
        await _sessions.SaveChangesAsync(ct);

        var tally = await _sessions.GetTallyAsync(sessionId, ct);
        return new VoteTallyDto(sessionId, tally, tally.Values.Sum());
    }

    // ── Close ────────────────────────────────────────────────────────────────

    public async Task<VoteSessionDto> CloseSessionAsync(
        Guid              sessionId,
        Guid              groupId,
        Guid              hostUserId,
        CancellationToken ct = default)
    {
        var group = await _groups.FindByIdAsync(groupId, ct)
            ?? throw new InvalidOperationException("Group not found.");

        if (group.HostUserId != hostUserId)
            throw new DomainException("Only the group host can close a vote.");

        var session = await _sessions.FindByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException("Vote session not found.");

        session.Close(randomTiebreak: true);
        await _sessions.SaveChangesAsync(ct);

        return ToDto(session);
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private static VoteSessionDto ToDto(VoteSession s) =>
        new(s.Id, s.GroupId, s.Status, s.CandidateGameIds, s.WinnerGameId, s.StartedAt, s.ClosedAt);
}
