namespace PlayForge.Infrastructure.Caching;

public static class CacheKeys
{
    public static string UserLibrary(Guid userId)       => $"library:{userId}";
    public static string UserFriends(Guid userId)       => $"friends:{userId}";
    public static string GroupCommonGames(Guid groupId) => $"common-games:{groupId}";
}
