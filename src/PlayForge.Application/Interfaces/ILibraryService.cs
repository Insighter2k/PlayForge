using PlayForge.Application.DTOs;

namespace PlayForge.Application.Interfaces;

public interface ILibraryService
{
    Task<IReadOnlyList<GameDto>> SyncAndGetLibraryAsync(Guid userId, CancellationToken ct = default);
}
