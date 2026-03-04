using PlayForge.Application.DTOs;

namespace PlayForge.Application.Interfaces;

public interface IFriendService
{
    Task<IReadOnlyList<FriendDto>> GetFriendsAsync(Guid userId, CancellationToken ct = default);
}
