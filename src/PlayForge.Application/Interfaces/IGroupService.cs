using PlayForge.Application.DTOs;

namespace PlayForge.Application.Interfaces;

public interface IGroupService
{
    Task<GroupDto> CreateGroupAsync(Guid hostUserId, string groupName, CancellationToken ct = default);
    Task           DeleteGroupAsync(Guid groupId, Guid requestingUserId, CancellationToken ct = default);
    Task<GroupDto> JoinGroupByCodeAsync(Guid userId, string inviteCode, CancellationToken ct = default);
    Task<IReadOnlyList<GroupSummaryDto>> GetUserGroupsAsync(Guid userId, CancellationToken ct = default);
    Task<GroupDto> GetGroupAsync(Guid groupId, CancellationToken ct = default);
    Task<IReadOnlyList<GameCandidateDto>>    GetGroupCommonGamesAsync(Guid groupId, CancellationToken ct = default);
    Task<IReadOnlyList<UserDto>>             GetGroupMembersAsync(Guid groupId, CancellationToken ct = default);

    Task<IReadOnlyList<GameSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default);
    Task<GroupCandidateDto>                  AddGroupCandidateAsync(Guid groupId, Guid addedByUserId, long appId, string name, string coverImageUrl, CancellationToken ct = default);
    Task                                     RemoveGroupCandidateAsync(Guid groupId, Guid candidateId, CancellationToken ct = default);
    Task<IReadOnlyList<GroupCandidateDto>>   GetGroupCandidatesAsync(Guid groupId, CancellationToken ct = default);
}
