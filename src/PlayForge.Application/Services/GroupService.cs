using PlayForge.Application.DTOs;
using PlayForge.Application.Interfaces;
using PlayForge.Domain.Entities;
using PlayForge.Domain.Enums;
using PlayForge.Domain.Exceptions;
using PlayForge.Domain.Interfaces;

namespace PlayForge.Application.Services;

public class GroupService : IGroupService
{
    private readonly IGroupRepository          _groups;
    private readonly IUserRepository           _users;
    private readonly IGameRepository           _games;
    private readonly IPlatformConnectorRegistry _registry;

    public GroupService(
        IGroupRepository           groups,
        IUserRepository            users,
        IGameRepository            games,
        IPlatformConnectorRegistry registry)
    {
        _groups   = groups;
        _users    = users;
        _games    = games;
        _registry = registry;
    }

    public async Task<GroupDto> CreateGroupAsync(
        Guid              hostUserId,
        string            groupName,
        CancellationToken ct = default)
    {
        var group = GameGroup.Create(hostUserId, groupName.Trim());
        await _groups.AddAsync(group, ct);
        await _groups.SaveChangesAsync(ct);
        return ToDto(group);
    }

    public async Task DeleteGroupAsync(
        Guid              groupId,
        Guid              requestingUserId,
        CancellationToken ct = default)
    {
        var group = await _groups.FindByIdAsync(groupId, ct)
            ?? throw new InvalidOperationException("Group not found.");

        if (group.HostUserId != requestingUserId)
            throw new DomainException("Only the group host can delete the group.");

        await _groups.DeleteAsync(groupId, ct);
    }

    public async Task<GroupDto> JoinGroupByCodeAsync(
        Guid              userId,
        string            inviteCode,
        CancellationToken ct = default)
    {
        var group = await _groups.FindByInviteCodeAsync(inviteCode.Trim().ToUpperInvariant(), ct)
            ?? throw new InvalidOperationException("Invite code not found.");

        group.AddMember(userId);
        await _groups.SaveChangesAsync(ct);
        return ToDto(group);
    }

    public async Task<IReadOnlyList<GroupSummaryDto>> GetUserGroupsAsync(
        Guid              userId,
        CancellationToken ct = default)
    {
        var groups = await _groups.FindByUserIdAsync(userId, ct);
        return groups
            .Select(g => new GroupSummaryDto(
                g.Id,
                g.Name,
                g.Memberships.Count,
                g.VoteSessions.Any(vs => vs.Status == VoteSessionStatus.Active)))
            .ToList();
    }

    public async Task<GroupDto> GetGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        var group = await _groups.FindByIdAsync(groupId, ct)
            ?? throw new InvalidOperationException($"Group {groupId} not found.");
        return ToDto(group);
    }

    public async Task<IReadOnlyList<UserDto>> GetGroupMembersAsync(
        Guid              groupId,
        CancellationToken ct = default)
    {
        var group = await _groups.FindByIdAsync(groupId, ct)
            ?? throw new InvalidOperationException($"Group {groupId} not found.");

        var memberIds = group.Memberships.Select(m => m.UserId).ToList();
        var users     = await _users.FindByIdsAsync(memberIds, ct);

        return users
            .Select(u => new UserDto(u.Id, u.SteamId.Value.ToString(), u.DisplayName, u.AvatarUrl))
            .OrderBy(u => u.DisplayName)
            .ToList();
    }

    public async Task<IReadOnlyList<GameCandidateDto>> GetGroupCommonGamesAsync(
        Guid              groupId,
        CancellationToken ct = default)
    {
        var group = await _groups.FindByIdAsync(groupId, ct)
            ?? throw new InvalidOperationException($"Group {groupId} not found.");

        var memberIds        = group.Memberships.Select(m => m.UserId).ToList();
        var members          = await _users.FindByIdsAsync(memberIds, ct);
        var membersWithGames = members.Where(u => u.Games.Any()).ToList();

        if (membersWithGames.Count == 0) return [];

        var commonGameIds = membersWithGames
            .Select(u => u.Games.Select(ug => ug.GameId).ToHashSet())
            .Aggregate((a, b) => { a.IntersectWith(b); return a; });

        if (commonGameIds.Count == 0) return [];

        var gamesById = await _games.FindByIdsAsync(commonGameIds, ct);

        return gamesById.Values
            .Select(g => new GameCandidateDto(
                g.Id, g.AppId.Value, g.Name, g.CoverImageUrl, g.Tags,
                g.Screenshots, g.ReleaseDate, g.ReviewSummary, g.ReviewScore))
            .OrderBy(g => g.Name)
            .ToList();
    }

    public async Task<IReadOnlyList<GameSearchResultDto>> SearchGamesAsync(
        string            query,
        CancellationToken ct = default)
    {
        var results = await _registry.Get(Platform.Steam).SearchGamesAsync(query, 10, ct);
        return results
            .Select(r => new GameSearchResultDto(r.AppId, r.Name, r.CoverImageUrl))
            .ToList();
    }

    public async Task<GroupCandidateDto> AddGroupCandidateAsync(
        Guid              groupId,
        Guid              addedByUserId,
        long              appId,
        string            name,
        string            coverImageUrl,
        CancellationToken ct = default)
    {
        // Return existing if already added (idempotent)
        var existing = await _groups.FindCandidateByAppIdAsync(groupId, appId, ct);
        if (existing is not null) return ToCandidateDto(existing);

        var candidate = GroupCandidate.Create(groupId, addedByUserId, appId, name, coverImageUrl);
        await _groups.AddCandidateAsync(candidate, ct);
        await _groups.SaveChangesAsync(ct);

        return ToCandidateDto(candidate);
    }

    public async Task RemoveGroupCandidateAsync(
        Guid              groupId,
        Guid              candidateId,
        CancellationToken ct = default)
    {
        await _groups.RemoveCandidateAsync(groupId, candidateId, ct);
        await _groups.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<GroupCandidateDto>> GetGroupCandidatesAsync(
        Guid              groupId,
        CancellationToken ct = default)
    {
        var group = await _groups.FindByIdAsync(groupId, ct)
            ?? throw new InvalidOperationException($"Group {groupId} not found.");

        return group.Candidates
            .Select(ToCandidateDto)
            .OrderBy(c => c.Name)
            .ToList();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static GroupDto ToDto(GameGroup g) =>
        new(g.Id, g.Name, g.InviteCode, g.HostUserId, g.Memberships.Count);

    private static GroupCandidateDto ToCandidateDto(GroupCandidate c) =>
        new(c.Id, c.AddedByUserId, c.AppId, c.Name, c.CoverImageUrl);
}
